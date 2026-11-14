using System.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuranAnchor.Application.Abstractions;
using QuranAnchor.Domain.Constants;
using QuranAnchor.Domain.Entities;
using QuranAnchor.Domain.Enums;
using QuranAnchor.Infrastructure.Persistence;
using QuranAnchor.Presentation.Helpers;
using QuranAnchor.Presentation.Services;

namespace QuranAnchor.Presentation.Controllers;

/// <summary>Class fee invoices and balances. Class fees are paid outside the portal; staff record what was received.</summary>
[Authorize]
public class InvoicesController(
    QuranAnchorDbContext db,
    IIdGenerator ids,
    INotificationService notify,
    ISettingsService settings,
    IAuditLogger audit,
    CurrentUser me) : Controller
{
    private static bool TryPeriod(string? period, out int year, out int month)
    {
        year = month = 0;
        var parts = (period ?? "").Split('-');
        return parts.Length == 2 && int.TryParse(parts[0], out year) && int.TryParse(parts[1], out month) && month is >= 1 and <= 12 && year is >= 2020 and <= 2100;
    }

    private async Task<string> PayInstructionsHtmlAsync()
    {
        var brand = await settings.GetBrandingAsync();
        return string.IsNullOrWhiteSpace(brand.FeeInstructions)
            ? "Please follow the payment instructions you were given in the class group."
            : "How to pay: " + WebUtility.HtmlEncode(brand.FeeInstructions).Replace("\n", "<br />");
    }

    private async Task EmailInvoiceAsync(Invoice invoice, StudentProfile student)
    {
        var body = await notify.WrapAsync($"Class fee invoice {invoice.Number}",
            $"{WebUtility.HtmlEncode(invoice.Description)} for <strong>{WebUtility.HtmlEncode(student.FullName)}</strong>.",
            $"Amount: <strong>{Fmt.Money(invoice.Currency, invoice.Amount)}</strong>. Due on <strong>{invoice.DueDate:d MMM yyyy}</strong>.",
            await PayInstructionsHtmlAsync());
        await notify.SendToParentAsync(student, $"Class fee invoice {invoice.Number}", body);
    }

    // ---------- List ----------

    public async Task<IActionResult> Index(string? status, string? q, int page = 1)
    {
        if (!me.IsStaff && !me.IsStudent) return Forbid();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var scope = db.Invoices.AsQueryable();
        if (me.IsStudent)
        {
            var student = await me.StudentAsync();
            scope = scope.Where(i => i.StudentId == student!.Id && !i.IsCancelled);
        }

        var query = scope.AsNoTracking().Include(i => i.Student).Include(i => i.Payments).AsQueryable();
        if (!string.IsNullOrWhiteSpace(q)) query = query.Where(i => i.Student!.FullName.Contains(q) || i.Number.Contains(q));
        query = status switch
        {
            "open" => query.Where(i => !i.IsCancelled && (i.Payments.Sum(p => (decimal?)p.Amount) ?? 0) < i.Amount),
            "overdue" => query.Where(i => !i.IsCancelled && i.DueDate < today && (i.Payments.Sum(p => (decimal?)p.Amount) ?? 0) < i.Amount),
            "paid" => query.Where(i => !i.IsCancelled && (i.Payments.Sum(p => (decimal?)p.Amount) ?? 0) >= i.Amount),
            "cancelled" => query.Where(i => i.IsCancelled),
            _ => query
        };

        var (items, info) = await query.OrderByDescending(i => i.CreatedAtUtc).PageAsync(page);

        var openInvoices = await scope.AsNoTracking().Include(i => i.Payments).Where(i => !i.IsCancelled).ToListAsync();
        ViewBag.Outstanding = openInvoices.Where(i => i.Balance > 0).GroupBy(i => i.Currency)
            .ToDictionary(g => g.Key, g => g.Sum(i => i.Balance));
        ViewBag.PageInfo = info; ViewBag.Status = status; ViewBag.Q = q; ViewBag.Today = today;
        return View(items);
    }

    public async Task<IActionResult> Details(Guid id)
    {
        var invoice = await db.Invoices.AsNoTracking().Include(i => i.Student).Include(i => i.Payments).FirstOrDefaultAsync(i => i.Id == id);
        if (invoice?.Student is null) return NotFound();
        if (me.IsStudent && ((await me.StudentAsync())?.Id != invoice.StudentId || invoice.IsCancelled)) return NotFound();
        if (!me.IsStudent && !me.IsStaff) return Forbid();

        ViewBag.Brand = await settings.GetBrandingAsync();
        return View(invoice);
    }

    // ---------- Create ----------

    [Authorize(Policy = Permissions.ManageInvoices)]
    [HttpGet]
    public async Task<IActionResult> Create(Guid? studentId)
    {
        ViewBag.Students = await db.Students.AsNoTracking().Where(s => s.Status != AccountStatus.Deactivated).OrderBy(s => s.FullName).ToListAsync();
        ViewBag.StudentId = studentId;
        return View();
    }

    [Authorize(Policy = Permissions.ManageInvoices)]
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Guid studentId, string period, decimal amount, string currency, DateOnly dueDate, string? description)
    {
        var student = await db.Students.FirstOrDefaultAsync(s => s.Id == studentId);
        if (student is null || !TryPeriod(period, out var year, out var month) || amount <= 0)
        {
            TempData["Warning"] = "Choose a student, a month and an amount greater than zero.";
            return RedirectToAction(nameof(Create), new { studentId });
        }

        var invoice = new Invoice
        {
            StudentId = studentId, Number = await ids.NextInvoiceNumberAsync(), Year = year, Month = month,
            Currency = currency, Amount = amount, DueDate = dueDate,
            Description = string.IsNullOrWhiteSpace(description) ? $"Class fee for {Fmt.Month(year, month)}" : description.Trim()
        };
        db.Invoices.Add(invoice);
        await db.SaveChangesAsync();
        await audit.LogAsync("InvoiceCreated", invoice.Number, student.StudentCode);
        await EmailInvoiceAsync(invoice, student);

        TempData["Saved"] = "Invoice created and emailed to the parent.";
        return RedirectToAction(nameof(Details), new { id = invoice.Id });
    }

    /// <summary>Makes an invoice for every active student with a monthly class fee, if they do not have one for that month yet.</summary>
    [Authorize(Policy = Permissions.ManageInvoices)]
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Generate(string period, DateOnly dueDate)
    {
        if (!TryPeriod(period, out var year, out var month))
        {
            TempData["Warning"] = "Choose a month.";
            return RedirectToAction(nameof(Index));
        }

        var students = await db.Students.Where(s => s.Status == AccountStatus.Active && s.ClassFeeAmount > 0).ToListAsync();
        var already = (await db.Invoices.Where(i => i.Year == year && i.Month == month && !i.IsCancelled).Select(i => i.StudentId).ToListAsync()).ToHashSet();

        var created = 0;
        foreach (var student in students.Where(s => !already.Contains(s.Id)))
        {
            var invoice = new Invoice
            {
                StudentId = student.Id, Number = await ids.NextInvoiceNumberAsync(), Year = year, Month = month,
                Currency = student.ClassFeeCurrency, Amount = student.ClassFeeAmount, DueDate = dueDate,
                Description = $"Class fee for {Fmt.Month(year, month)}"
            };
            db.Invoices.Add(invoice);
            await db.SaveChangesAsync();
            await EmailInvoiceAsync(invoice, student);
            created++;
        }

        await audit.LogAsync("InvoicesGenerated", Fmt.Month(year, month), $"{created} invoice(s)");
        TempData["Saved"] = created == 0
            ? "Nothing to create. Every student with a class fee already has an invoice for that month."
            : $"{created} invoice(s) created and emailed.";
        return RedirectToAction(nameof(Index));
    }

    // ---------- Payments received ----------

    [Authorize(Policy = Permissions.ManageInvoices)]
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> RecordPayment(Guid id, decimal amount, DateOnly paidOn, string method, string? reference)
    {
        var invoice = await db.Invoices.Include(i => i.Student).Include(i => i.Payments).FirstOrDefaultAsync(i => i.Id == id);
        if (invoice?.Student is null) return NotFound();
        if (invoice.IsCancelled || amount <= 0)
        {
            TempData["Warning"] = "Enter an amount greater than zero on an invoice that is not cancelled.";
            return RedirectToAction(nameof(Details), new { id });
        }

        invoice.Payments.Add(new InvoicePayment
        {
            Amount = amount, PaidOn = paidOn, Method = string.IsNullOrWhiteSpace(method) ? "Bank transfer" : method.Trim(),
            Reference = reference?.Trim() ?? "", RecordedBy = User.FindFirst(AppClaimTypes.FullName)?.Value ?? User.Identity?.Name ?? ""
        });
        await db.SaveChangesAsync();
        await audit.LogAsync("InvoicePaymentRecorded", invoice.Number, $"{invoice.Currency} {amount:0.00}");

        var balance = invoice.Balance;
        var body = await notify.WrapAsync(balance <= 0 ? "Class fee paid in full" : "Payment received",
            $"We received <strong>{Fmt.Money(invoice.Currency, amount)}</strong> for invoice {invoice.Number} ({WebUtility.HtmlEncode(invoice.Student.FullName)}).",
            balance <= 0 ? "The invoice is now fully paid. Thank you." : $"The balance left is <strong>{Fmt.Money(invoice.Currency, balance)}</strong>.");
        await notify.SendToParentAsync(invoice.Student, balance <= 0 ? "Class fee paid in full" : "Payment received", body);

        TempData["Saved"] = "Payment recorded and the parent was emailed.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [Authorize(Policy = Permissions.ManageInvoices)]
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(Guid id)
    {
        var invoice = await db.Invoices.FirstOrDefaultAsync(i => i.Id == id);
        if (invoice is null) return NotFound();
        invoice.IsCancelled = !invoice.IsCancelled;
        await db.SaveChangesAsync();
        await audit.LogAsync(invoice.IsCancelled ? "InvoiceCancelled" : "InvoiceRestored", invoice.Number);
        TempData["Saved"] = invoice.IsCancelled ? "Invoice cancelled." : "Invoice restored.";
        return RedirectToAction(nameof(Details), new { id });
    }
}
