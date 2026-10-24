using System.Text;
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
public class EarningsController(
    QuranAnchorDbContext db,
    IReportService reports,
    CurrentUser me) : Controller
{
    /// <summary>A tutor's own earnings. The total starts again on the 1st; earlier months stay in history.</summary>
    public async Task<IActionResult> Mine(int? year, int? month)
    {
        var tutor = await me.TutorAsync();
        if (tutor is null) return Forbid();

        var today = TimeHelper.LocalToday(tutor.TimeZoneId);
        var y = year ?? today.Year; var m = month ?? today.Month;
        var first = new DateOnly(y, m, 1);
        var from = TimeHelper.ToUtc(first, TimeOnly.MinValue, tutor.TimeZoneId);
        var to = TimeHelper.ToUtc(first.AddMonths(1), TimeOnly.MinValue, tutor.TimeZoneId);

        ViewBag.Sessions = await db.Sessions.AsNoTracking().Include(s => s.Student)
            .Where(s => s.TutorId == tutor.Id && s.Status == SessionStatus.Completed && s.StartUtc >= from && s.StartUtc < to)
            .OrderBy(s => s.StartUtc).ToListAsync();
        ViewBag.Summary = (await reports.TutorSummariesAsync(y, m, tutor.Id)).FirstOrDefault();
        ViewBag.Year = y; ViewBag.Month = m; ViewBag.Zone = tutor.TimeZoneId;
        return View();
    }

    [Authorize(Policy = Permissions.ViewAllEarnings)]
    public async Task<IActionResult> Summary(int? year, int? month)
    {
        var now = DateTime.UtcNow;
        var y = year ?? now.Year; var m = month ?? now.Month;
        ViewBag.Year = y; ViewBag.Month = m;
        return View(await reports.TutorSummariesAsync(y, m, null));
    }

    [Authorize(Policy = Permissions.ViewAllEarnings)]
    public async Task<IActionResult> Csv(int year, int month)
    {
        var rows = await reports.TutorSummariesAsync(year, month, null);
        var sb = new StringBuilder("Tutor,Students taught,Sessions held,Sessions not held,Hours,Feedback missing,Amount\n");
        foreach (var r in rows)
        {
            var name = r.TutorName.Replace("\"", "\"\"");
            sb.AppendLine($"\"{name}\",{r.StudentsTaught},{r.SessionsHeld},{r.SessionsNotHeld},{r.Hours},{r.FeedbackMissing},{r.Amount:0.00}");
        }
        // The byte order mark lets Excel read names with Arabic or accents correctly.
        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
        return File(bytes, "text/csv", $"Tutor-summary-{year}-{month:D2}.csv");
    }
}
