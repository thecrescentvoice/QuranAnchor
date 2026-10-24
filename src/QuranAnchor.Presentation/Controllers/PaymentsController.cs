using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuranAnchor.Application.Abstractions;
using QuranAnchor.Application.Common;
using QuranAnchor.Domain.Constants;
using QuranAnchor.Domain.Enums;
using QuranAnchor.Infrastructure.Persistence;
using QuranAnchor.Presentation.Services;

namespace QuranAnchor.Presentation.Controllers;

[Authorize]
public class PaymentsController(
    QuranAnchorDbContext db,
    IPaymentService payments,
    ISettingsService settings,
    IAuditLogger audit,
    CurrentUser me) : Controller
{
    // ---------- The parent's pay page ----------

    public async Task<IActionResult> Index()
    {
        if (!me.IsStudent) return me.Can(Permissions.ViewPayments) ? RedirectToAction(nameof(All)) : Forbid();

        var student = await me.StudentAsync();
        if (student is null) return Forbid();
        ViewBag.Fee = FeeRules.StateOf(student, await settings.GetSystemAsync());
        ViewBag.Prices = await db.PortalPrices.AsNoTracking().Where(p => p.IsActive).OrderBy(p => p.Currency).ToListAsync();
        ViewBag.History = await db.Payments.AsNoTracking().Where(p => p.StudentId == student.Id && p.Status == PaymentStatus.Succeeded).OrderByDescending(p => p.PaidAtUtc).Take(24).ToListAsync();
        return View(student);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Pay(string currency)
    {
        var student = await me.StudentAsync();
        if (student is null) return Forbid();

        var callback = Url.Action(nameof(Callback), "Payments", null, Request.Scheme)!;
        var result = await payments.StartCheckoutAsync(student.Id, currency, callback);
        if (!result.Ok || result.Url is null)
        {
            TempData["Warning"] = result.Error;
            return RedirectToAction(nameof(Index));
        }
        await audit.LogAsync("PaymentStarted", student.StudentCode, currency);
        return Redirect(result.Url);
    }

    /// <summary>Where Paystack sends the parent back. We confirm the payment with Paystack instead of trusting this address.</summary>
    [HttpGet]
    public async Task<IActionResult> Callback(string? reference, string? trxref)
    {
        var refToCheck = reference ?? trxref;
        if (string.IsNullOrWhiteSpace(refToCheck)) return RedirectToAction(nameof(Index));

        var result = await payments.VerifyAsync(refToCheck);
        TempData[result.Ok ? "Saved" : "Warning"] = result.Message;
        return RedirectToAction(nameof(Index));
    }

    /// <summary>Paystack's own confirmation message. It is trusted only if the signature matches.</summary>
    [HttpPost, AllowAnonymous, IgnoreAntiforgeryToken]
    public async Task<IActionResult> Webhook()
    {
        using var reader = new StreamReader(Request.Body);
        var body = await reader.ReadToEndAsync();
        var signature = Request.Headers["x-paystack-signature"].ToString();
        return await payments.HandleWebhookAsync(body, signature) ? Ok() : BadRequest();
    }

    // ---------- Staff ----------

    [Authorize(Policy = Permissions.ViewPayments)]
    public async Task<IActionResult> All()
    {
        var list = await db.Payments.AsNoTracking().Include(p => p.Student).OrderByDescending(p => p.CreatedAtUtc).Take(150).ToListAsync();
        return View(list);
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = Permissions.MarkFeePaid)]
    public async Task<IActionResult> MarkPaid(Guid studentId, string? note)
    {
        await payments.MarkManualAsync(studentId, string.IsNullOrWhiteSpace(note) ? "Marked paid by staff" : note);
        await audit.LogAsync("FeeMarkedPaid", studentId.ToString(), note);
        TempData["Saved"] = "Marked as paid for one month. A receipt was emailed to the parent.";
        return RedirectToAction("Details", "Students", new { id = studentId });
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = Permissions.MarkFeePaid)]
    public async Task<IActionResult> ToggleExempt(Guid studentId)
    {
        var s = await db.Students.FirstOrDefaultAsync(x => x.Id == studentId);
        if (s is null) return NotFound();
        s.IsFeeExempt = !s.IsFeeExempt;
        await db.SaveChangesAsync();
        await audit.LogAsync("FeeExemptChanged", s.StudentCode, s.IsFeeExempt ? "Exempt" : "Not exempt");
        TempData["Saved"] = s.IsFeeExempt ? "The portal fee is waived for this student." : "The portal fee applies again.";
        return RedirectToAction("Details", "Students", new { id = studentId });
    }
}
