using ClosedXML.Excel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuranAnchor.Application.Abstractions;
using QuranAnchor.Application.Common;
using QuranAnchor.Domain.Constants;
using QuranAnchor.Domain.Enums;
using QuranAnchor.Infrastructure.Persistence;

namespace QuranAnchor.Presentation.Controllers;

/// <summary>Downloads of the academy's records as Excel files.</summary>
[Authorize(Policy = Permissions.ExportData)]
public class ExportsController(
    QuranAnchorDbContext db,
    IReportService reports,
    IAuditLogger audit) : Controller
{
    public IActionResult Index() => View();

    // ---------- The workbook ----------

    private static object? Plain(object? v) => v switch
    {
        null => null,
        DateOnly d => d.ToDateTime(TimeOnly.MinValue),
        Enum e => e.ToString(),
        Guid g => g.ToString(),
        decimal m => (double)m,
        _ => v
    };

    private IActionResult Sheet(string title, string fileName, string[] headers, IEnumerable<object?[]> rows)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add(title);

        for (var c = 0; c < headers.Length; c++) sheet.Cell(1, c + 1).Value = headers[c];
        var headerRow = sheet.Range(1, 1, 1, headers.Length);
        headerRow.Style.Font.Bold = true;
        headerRow.Style.Fill.BackgroundColor = XLColor.FromHtml("#F1F3FA");

        var r = 2;
        foreach (var row in rows)
        {
            for (var c = 0; c < row.Length; c++)
            {
                var value = Plain(row[c]);
                var cell = sheet.Cell(r, c + 1);
                switch (value)
                {
                    case null: break;
                    case string s: cell.Value = s; break;
                    case double d: cell.Value = d; break;
                    case int i: cell.Value = i; break;
                    case long l: cell.Value = l; break;
                    case bool b: cell.Value = b; break;
                    case DateTime dt: cell.Value = dt; cell.Style.DateFormat.Format = dt.TimeOfDay == TimeSpan.Zero ? "yyyy-mm-dd" : "yyyy-mm-dd hh:mm"; break;
                    default: cell.Value = value.ToString() ?? ""; break;
                }
            }
            r++;
        }

        sheet.SheetView.FreezeRows(1);
        sheet.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
    }

    // ---------- The downloads ----------

    public async Task<IActionResult> Students()
    {
        var list = await db.Students.AsNoTracking().Include(s => s.Tutor).OrderBy(s => s.FullName).ToListAsync();
        await audit.LogAsync("ExportDownloaded", "Students", $"{list.Count} rows");
        return Sheet("Students", $"Students-{DateTime.UtcNow:yyyy-MM-dd}.xlsx",
            new[] { "Student ID", "Name", "Level", "Status", "Tutor", "Parent", "Parent email", "Phone", "Country", "Time zone", "Sessions per month", "Class fee", "Class fee currency", "Portal fee paid until", "Fee waived", "Joined" },
            list.Select(s => new object?[] { s.StudentCode, s.FullName, s.Level, s.Status, s.Tutor?.FullName, s.ParentName, s.ParentEmail, s.Phone, s.Country, s.TimeZoneId,
                s.SessionsPerMonth, s.ClassFeeAmount, s.ClassFeeCurrency, s.PaidUntilUtc, s.IsFeeExempt, s.CreatedAtUtc }));
    }

    public async Task<IActionResult> Payments(DateOnly from, DateOnly to)
    {
        var fromUtc = from.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var toUtc = to.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc);
        var list = await db.Payments.AsNoTracking().Include(p => p.Student).Where(p => p.CreatedAtUtc >= fromUtc && p.CreatedAtUtc <= toUtc).OrderBy(p => p.CreatedAtUtc).ToListAsync();
        await audit.LogAsync("ExportDownloaded", "Portal fee payments", $"{from:yyyy-MM-dd} to {to:yyyy-MM-dd}");
        return Sheet("Portal fee payments", $"Portal-fee-payments-{from:yyyy-MM-dd}-to-{to:yyyy-MM-dd}.xlsx",
            new[] { "Date", "Student ID", "Student", "Type", "Reference", "Currency", "Amount", "Status", "Paid at", "Period start", "Period end", "Note" },
            list.Select(p => new object?[] { p.CreatedAtUtc, p.Student?.StudentCode, p.Student?.FullName, p.IsManual ? "Manual" : p.Provider, p.Reference, p.Currency, p.Amount, p.Status, p.PaidAtUtc, p.PeriodStartUtc, p.PeriodEndUtc, p.Note }));
    }

    public async Task<IActionResult> Classes(int year, int month)
    {
        var first = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Utc);
        var list = await db.Sessions.AsNoTracking().Include(s => s.Student).Include(s => s.Tutor).Include(s => s.Feedback)
            .Where(s => s.StartUtc >= first.AddDays(-1) && s.StartUtc < first.AddMonths(1).AddDays(2)).OrderBy(s => s.StartUtc).ToListAsync();

        // Keep the classes whose date, in the student's own time zone, falls in the chosen month.
        var inMonth = list.Where(s => { var d = TimeHelper.ToLocal(s.StartUtc, s.Student?.TimeZoneId ?? "Africa/Lagos"); return d.Year == year && d.Month == month; }).ToList();
        await audit.LogAsync("ExportDownloaded", "Classes", $"{year}-{month:D2}");
        return Sheet("Classes and attendance", $"Classes-{year}-{month:D2}.xlsx",
            new[] { "Date", "Student ID", "Student", "Tutor", "Minutes", "Status", "Reason if not held", "Used a session", "Extra class", "Tutor earned", "Lesson", "Goal achieved", "Student on time" },
            inMonth.Select(s => new object?[] { TimeHelper.ToLocal(s.StartUtc, s.Student?.TimeZoneId ?? "Africa/Lagos"), s.Student?.StudentCode, s.Student?.FullName, s.Tutor?.FullName,
                s.DurationMinutes, s.Status, s.NotHeldReason, s.UsesPackageSession, s.IsExtra, s.AmountEarned, s.Feedback?.LessonTitle, s.Feedback?.Goal, s.Feedback?.StudentOnTime }));
    }

    public async Task<IActionResult> TutorSummary(int year, int month)
    {
        var rows = await reports.TutorSummariesAsync(year, month, null);
        await audit.LogAsync("ExportDownloaded", "Tutor summary", $"{year}-{month:D2}");
        return Sheet("Tutor summary", $"Tutor-summary-{year}-{month:D2}.xlsx",
            new[] { "Tutor", "Students taught", "Classes held", "Classes not held", "Hours", "Feedback missing", "Amount" },
            rows.Select(r => new object?[] { r.TutorName, r.StudentsTaught, r.SessionsHeld, r.SessionsNotHeld, r.Hours, r.FeedbackMissing, r.Amount }));
    }

    public async Task<IActionResult> Invoices(int? year, int? month)
    {
        var query = db.Invoices.AsNoTracking().Include(i => i.Student).Include(i => i.Payments).AsQueryable();
        if (year is not null && month is not null) query = query.Where(i => i.Year == year && i.Month == month);
        var list = await query.OrderBy(i => i.Number).ToListAsync();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        await audit.LogAsync("ExportDownloaded", "Invoices", year is null ? "All" : $"{year}-{month:D2}");
        return Sheet("Class fee invoices", year is null ? "Invoices-all.xlsx" : $"Invoices-{year}-{month:D2}.xlsx",
            new[] { "Number", "Student ID", "Student", "For month", "Currency", "Amount", "Paid", "Balance", "Due", "Status" },
            list.Select(i => new object?[] { i.Number, i.Student?.StudentCode, i.Student?.FullName, $"{i.Year}-{i.Month:D2}", i.Currency, i.Amount, i.PaidAmount, i.Balance, i.DueDate, i.StatusText(today) }));
    }
}
