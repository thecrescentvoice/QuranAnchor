using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuranAnchor.Application.Abstractions;
using QuranAnchor.Application.Common;
using QuranAnchor.Domain.Constants;
using QuranAnchor.Domain.Entities;
using QuranAnchor.Domain.Enums;
using QuranAnchor.Infrastructure.Persistence;
using QuranAnchor.Presentation.Services;
using QuranAnchor.Presentation.ViewModels;

namespace QuranAnchor.Presentation.Controllers;

[Authorize]
public class PlansController(
    QuranAnchorDbContext db,
    IPdfService pdf,
    ISettingsService settings,
    IAuditLogger audit,
    CurrentUser me) : Controller
{
    private static readonly Regex RangePattern = new(@"^(\d+)\s*(?:[-–]\s*(\d+))?$", RegexOptions.Compiled);

    private async Task<bool> CanWriteAsync(StudentProfile s)
    {
        if (me.Can(Permissions.ManageSchedules)) return true;
        return me.IsTutor && s.TutorId == (await me.TutorAsync())?.Id;
    }

    public async Task<IActionResult> Index()
    {
        var query = db.Plans.AsNoTracking().Include(p => p.Student).AsQueryable();
        if (me.IsStudent)
        {
            var student = await me.StudentAsync();
            query = query.Where(p => p.StudentId == student!.Id && p.Status == PublishStatus.Published);
        }
        else if (me.IsTutor)
        {
            var tutor = await me.TutorAsync();
            query = query.Where(p => p.TutorId == tutor!.Id);
        }
        return View(await query.OrderByDescending(p => p.Year).ThenByDescending(p => p.Month).Take(100).ToListAsync());
    }

    // ---------- Write ----------

    [HttpGet]
    public async Task<IActionResult> Edit(Guid studentId, int? year, int? month)
    {
        var student = await db.Students.AsNoTracking().FirstOrDefaultAsync(s => s.Id == studentId);
        if (student is null) return NotFound();
        if (!await CanWriteAsync(student)) return Forbid();

        var today = TimeHelper.LocalToday(student.TimeZoneId);
        var y = year ?? today.Year; var m = month ?? today.Month;
        var plan = await db.Plans.AsNoTracking().Include(p => p.Rows).FirstOrDefaultAsync(p => p.StudentId == studentId && p.Year == y && p.Month == m);
        var rules = await settings.GetSystemAsync();

        var model = new PlanFormViewModel { StudentId = studentId, StudentName = student.FullName, Year = y, Month = m };
        if (plan is null)
        {
            model.PostClassExpectations = rules.PlanExpectationsTemplate;
            model.ParentNotes = rules.PlanParentNotesTemplate;
            model.ClassFormat = await ClassFormatAsync(studentId, today);

            // Start from last month's plan when there is one.
            var prev = new DateOnly(y, m, 1).AddMonths(-1);
            var last = await db.Plans.AsNoTracking().Include(p => p.Rows).FirstOrDefaultAsync(p => p.StudentId == studentId && p.Year == prev.Year && p.Month == prev.Month);
            if (last is not null)
            {
                model.Goal = last.Goal; model.MonthGoals = last.MonthGoals; model.PostClassExpectations = last.PostClassExpectations;
                model.DailyTilawahFocus = last.DailyTilawahFocus; model.ParentNotes = last.ParentNotes; model.RowsText = RowsToText(last.Rows);
            }
        }
        else
        {
            model.Status = plan.Status; model.Goal = plan.Goal; model.MonthGoals = plan.MonthGoals; model.PostClassExpectations = plan.PostClassExpectations;
            model.DailyTilawahFocus = plan.DailyTilawahFocus; model.ClassFormat = plan.ClassFormat; model.ParentNotes = plan.ParentNotes; model.RowsText = RowsToText(plan.Rows);
        }
        return View(model);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(PlanFormViewModel model)
    {
        var student = await db.Students.FirstOrDefaultAsync(s => s.Id == model.StudentId);
        if (student is null) return NotFound();
        if (!await CanWriteAsync(student)) return Forbid();
        model.StudentName = student.FullName;

        var rows = ParseRows(model.RowsText, out var error);
        if (error is not null) ModelState.AddModelError(nameof(model.RowsText), error);
        if (!ModelState.IsValid) return View(model);

        var plan = await db.Plans.Include(p => p.Rows).FirstOrDefaultAsync(p => p.StudentId == model.StudentId && p.Year == model.Year && p.Month == model.Month);
        if (plan is null)
        {
            plan = new MonthlyPlan { StudentId = student.Id, TutorId = student.TutorId ?? Guid.Empty, Year = model.Year, Month = model.Month };
            if (student.TutorId is null) { ModelState.AddModelError(string.Empty, "Assign a tutor before writing a plan."); return View(model); }
            db.Plans.Add(plan);
        }

        plan.Goal = model.Goal ?? ""; plan.MonthGoals = model.MonthGoals ?? ""; plan.PostClassExpectations = model.PostClassExpectations ?? "";
        plan.DailyTilawahFocus = model.DailyTilawahFocus ?? ""; plan.ClassFormat = model.ClassFormat ?? ""; plan.ParentNotes = model.ParentNotes ?? "";

        db.PlanRows.RemoveRange(plan.Rows);
        plan.Rows = rows;
        await db.SaveChangesAsync();

        await audit.LogAsync("PlanSaved", student.StudentCode, Fmt(model.Year, model.Month));
        TempData["Saved"] = "Plan saved as a draft. Publish it when it is ready, so the parent can see it.";
        return RedirectToAction(nameof(Edit), new { studentId = model.StudentId, year = model.Year, month = model.Month });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Publish(Guid id)
    {
        var plan = await db.Plans.Include(p => p.Student).FirstOrDefaultAsync(p => p.Id == id);
        if (plan?.Student is null) return NotFound();
        if (!await CanWriteAsync(plan.Student)) return Forbid();

        plan.Status = PublishStatus.Published;
        plan.PublishedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync();
        await audit.LogAsync("PlanPublished", plan.Student.StudentCode, Fmt(plan.Year, plan.Month));
        TempData["Saved"] = "Plan published.";
        return RedirectToAction(nameof(Show), new { id });
    }

    /// <summary>Publish from the edit page, by student and month.</summary>
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> PublishFor(Guid studentId, int year, int month)
    {
        var plan = await db.Plans.Include(p => p.Student).FirstOrDefaultAsync(p => p.StudentId == studentId && p.Year == year && p.Month == month);
        if (plan?.Student is null)
        {
            TempData["Warning"] = "Save the plan first, then publish it.";
            return RedirectToAction(nameof(Edit), new { studentId, year, month });
        }
        return await Publish(plan.Id);
    }

    // ---------- Read ----------

    private async Task<(MonthlyPlan? Plan, IActionResult? Denied)> LoadForReadAsync(Guid id)
    {
        var plan = await db.Plans.AsNoTracking().Include(p => p.Student).Include(p => p.Tutor).Include(p => p.Rows).FirstOrDefaultAsync(p => p.Id == id);
        if (plan?.Student is null) return (null, NotFound());
        if (!await me.CanSeeStudentAsync(plan.Student)) return (null, Forbid());
        if (me.IsStudent)
        {
            if (plan.Status != PublishStatus.Published) return (null, NotFound());
            // Only the plan locks when the portal fee is overdue past the grace period. Join class never locks.
            if (FeeRules.StateOf(plan.Student, await settings.GetSystemAsync()) == FeeState.Locked) return (null, RedirectToAction("Locked"));
        }
        return (plan, null);
    }

    public async Task<IActionResult> Show(Guid id)
    {
        var (plan, denied) = await LoadForReadAsync(id);
        return denied ?? View(plan);
    }

    public async Task<IActionResult> Pdf(Guid id)
    {
        var (plan, denied) = await LoadForReadAsync(id);
        if (denied is not null) return denied;
        var bytes = await pdf.PlanPdfAsync(id);
        return File(bytes, "application/pdf", $"Monthly-Plan-{plan!.Student!.StudentCode}-{plan.Year}-{plan.Month:D2}.pdf");
    }

    public IActionResult Locked() => View();

    // ---------- Helpers ----------

    private static string Fmt(int y, int m) => $"{y}-{m:D2}";

    private static string RowsToText(IEnumerable<PlanRow> rows) =>
        string.Join("\n", rows.OrderBy(r => r.SessionFrom).Select(r =>
            $"{(r.SessionFrom == r.SessionTo ? r.SessionFrom.ToString() : $"{r.SessionFrom}-{r.SessionTo}")} | {r.Content} | {r.Activity} | {r.Murajaah}"));

    private static List<PlanRow> ParseRows(string? text, out string? error)
    {
        error = null;
        var rows = new List<PlanRow>();
        var n = 0;
        foreach (var raw in (text ?? "").Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            n++;
            var parts = raw.Split('|', StringSplitOptions.TrimEntries);
            var match = RangePattern.Match(parts[0]);
            if (!match.Success) { error = $"Line {n}: start with the session number, for example 1-2 | content | activity | muraja'ah."; return rows; }
            var from = int.Parse(match.Groups[1].Value);
            var to = match.Groups[2].Success ? int.Parse(match.Groups[2].Value) : from;
            if (to < from) { error = $"Line {n}: the second session number must not be smaller than the first."; return rows; }
            rows.Add(new PlanRow
            {
                SessionFrom = from, SessionTo = to,
                Content = parts.Length > 1 ? parts[1] : "", Activity = parts.Length > 2 ? parts[2] : "", Murajaah = parts.Length > 3 ? parts[3] : ""
            });
        }
        return rows;
    }

    /// <summary>"30-minute sessions on Tuesday and Saturday", made from the student's weekly schedule.</summary>
    private async Task<string> ClassFormatAsync(Guid studentId, DateOnly today)
    {
        var slots = await db.Schedules.AsNoTracking().Where(s => s.StudentId == studentId && (s.EffectiveTo == null || s.EffectiveTo >= today)).ToListAsync();
        if (slots.Count == 0) return string.Empty;

        var parts = slots.GroupBy(s => s.DurationMinutes).Select(g =>
        {
            var days = g.Select(x => x.DayOfWeek.ToString()).Distinct().ToList();
            var dayText = days.Count == 1 ? days[0] : string.Join(", ", days.Take(days.Count - 1)) + " and " + days[^1];
            return $"{g.Key}-minute sessions on {dayText}";
        });
        return string.Join("; ", parts);
    }
}
