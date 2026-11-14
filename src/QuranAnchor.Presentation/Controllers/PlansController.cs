using System.IO.Compression;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuranAnchor.Application.Abstractions;
using QuranAnchor.Application.Common;
using QuranAnchor.Domain.Constants;
using QuranAnchor.Domain.Entities;
using QuranAnchor.Domain.Enums;
using QuranAnchor.Infrastructure.Persistence;
using QuranAnchor.Presentation.Helpers;
using QuranAnchor.Presentation.Services;
using QuranAnchor.Presentation.ViewModels;

namespace QuranAnchor.Presentation.Controllers;

/// <summary>
/// Monthly plans. Tutors fill one grid for all their students (weeks 1 to 4, across Hifz, Qira'ah, Murāja'ah and Tajweed),
/// then download a PDF for each student. Parents see only published plans.
/// </summary>
[Authorize]
public class PlansController(
    QuranAnchorDbContext db,
    IPdfService pdf,
    ISettingsService settings,
    IAuditLogger audit,
    CurrentUser me) : Controller
{
    private const string NotApplicable = "Not Applicable";
    private const int Weeks = 4;
    private static readonly PlanTrack[] Tracks = { PlanTrack.Hifz, PlanTrack.Qiraah, PlanTrack.Murajaah, PlanTrack.Tajweed };

    private static string[] Lines(string? text) =>
        (text ?? string.Empty).Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    /// <summary>The students this person may write plans for: a tutor's own students, or everyone for staff.</summary>
    private async Task<List<StudentProfile>> WritableStudentsAsync(Guid? onlyStudent)
    {
        var query = db.Students.AsNoTracking().Include(s => s.Tutor).Where(s => s.Status != AccountStatus.Deactivated);
        if (me.IsTutor)
        {
            var tutor = await me.TutorAsync();
            if (tutor is null) return new List<StudentProfile>();
            query = query.Where(s => s.TutorId == tutor.Id);
        }
        if (onlyStudent is not null) query = query.Where(s => s.Id == onlyStudent);
        return await query.OrderBy(s => s.Tutor!.FullName).ThenBy(s => s.FullName).ToListAsync();
    }

    // ---------- The grid (tutors and staff) and the parent's list ----------

    public async Task<IActionResult> Index(int? year, int? month, Guid? studentId, bool copyLast = false)
    {
        if (me.IsStudent)
        {
            var student = await me.StudentAsync();
            var mine = student is null
                ? new List<MonthlyPlan>()
                : await db.Plans.AsNoTracking().Include(p => p.Student).Where(p => p.StudentId == student.Id && p.Status == PublishStatus.Published)
                    .OrderByDescending(p => p.Year).ThenByDescending(p => p.Month).ToListAsync();
            return View("StudentList", mine);
        }
        if (!me.IsStaff && !me.IsTutor) return Forbid();

        var rules = await settings.GetSystemAsync();
        var today = TimeHelper.ToLocal(DateTime.UtcNow, "Africa/Lagos");
        var y = year ?? today.Year;
        var m = Math.Clamp(month ?? today.Month, 1, 12);

        var students = await WritableStudentsAsync(studentId);
        var ids = students.Select(s => s.Id).ToList();
        var plans = await db.Plans.AsNoTracking().Include(p => p.Goals).Where(p => ids.Contains(p.StudentId) && p.Year == y && p.Month == m).ToListAsync();

        var previous = new List<MonthlyPlan>();
        if (copyLast)
        {
            var prev = new DateOnly(y, m, 1).AddMonths(-1);
            previous = await db.Plans.AsNoTracking().Include(p => p.Goals).Where(p => ids.Contains(p.StudentId) && p.Year == prev.Year && p.Month == prev.Month).ToListAsync();
        }

        var model = new PlanGridViewModel
        {
            Year = y, Month = m, OnlyStudentId = studentId, ShowTutor = me.IsStaff, CopiedFromLastMonth = copyLast,
            Options = new Dictionary<PlanTrack, string[]>
            {
                [PlanTrack.Hifz] = Lines(rules.PlanHifzOptions), [PlanTrack.Qiraah] = Lines(rules.PlanQiraahOptions),
                [PlanTrack.Murajaah] = Lines(rules.PlanMurajaahOptions), [PlanTrack.Tajweed] = Lines(rules.PlanTajweedOptions)
            }
        };

        foreach (var student in students)
        {
            var plan = plans.FirstOrDefault(p => p.StudentId == student.Id);
            // A student without a plan yet starts from last month's goals when "Copy from last month" was chosen.
            var source = plan ?? previous.FirstOrDefault(p => p.StudentId == student.Id);
            var row = new PlanGridRow { Student = student, Plan = plan };
            for (var week = 1; week <= Weeks; week++)
                foreach (var track in Tracks)
                    row.Values[$"{week}_{(int)track}"] = source?.Goals.FirstOrDefault(g => g.Week == week && g.Track == track)?.Text ?? NotApplicable;
            model.Rows.Add(row);
        }
        return View(model);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(int year, int month, string? mode, Guid? studentId)
    {
        if (!me.IsStaff && !me.IsTutor) return Forbid();
        var rules = await settings.GetSystemAsync();
        var students = await WritableStudentsAsync(studentId);

        var saved = 0;
        var skipped = new List<string>();
        foreach (var student in students)
        {
            if (!Request.Form.Keys.Any(k => k.StartsWith($"g_{student.Id}_", StringComparison.Ordinal))) continue;
            if (student.TutorId is null) { skipped.Add(student.FullName); continue; }

            var plan = await db.Plans.Include(p => p.Goals).FirstOrDefaultAsync(p => p.StudentId == student.Id && p.Year == year && p.Month == month);
            if (plan is null)
            {
                plan = new MonthlyPlan
                {
                    StudentId = student.Id, TutorId = student.TutorId.Value, Year = year, Month = month,
                    PostClassExpectations = rules.PlanExpectationsTemplate
                };
                db.Plans.Add(plan);
            }

            db.PlanGoals.RemoveRange(plan.Goals);
            plan.Goals = new List<PlanGoal>();
            for (var week = 1; week <= Weeks; week++)
            {
                foreach (var track in Tracks)
                {
                    var value = Request.Form[$"g_{student.Id}_{week}_{(int)track}"].ToString().Trim();
                    if (value.Length > 200) value = value[..200];
                    plan.Goals.Add(new PlanGoal { Week = week, Track = track, Text = value.Length == 0 ? NotApplicable : value });
                }
            }

            if (mode == "publish")
            {
                plan.Status = PublishStatus.Published;
                plan.PublishedAtUtc ??= DateTime.UtcNow;
            }
            saved++;
        }

        await db.SaveChangesAsync();
        await audit.LogAsync(mode == "publish" ? "PlansPublished" : "PlansSaved", $"{year}-{month:D2}", $"{saved} plan(s)");

        TempData["Saved"] = mode == "publish"
            ? $"{saved} plan(s) saved and published. Parents can now see them."
            : $"{saved} plan(s) saved as drafts.";
        if (skipped.Count > 0) TempData["Warning"] = "No tutor is assigned to: " + string.Join(", ", skipped) + ".";
        return RedirectToAction(nameof(Index), new { year, month, studentId });
    }

    // ---------- Downloads ----------

    /// <summary>One PDF for each student, in one zip file.</summary>
    public async Task<IActionResult> DownloadAll(int year, int month, Guid? studentId)
    {
        if (!me.IsStaff && !me.IsTutor) return Forbid();
        var students = await WritableStudentsAsync(studentId);
        var ids = students.Select(s => s.Id).ToList();
        var plans = await db.Plans.AsNoTracking().Include(p => p.Student)
            .Where(p => ids.Contains(p.StudentId) && p.Year == year && p.Month == month)
            .OrderBy(p => p.Student!.FullName).ToListAsync();

        if (plans.Count == 0)
        {
            TempData["Warning"] = "There are no saved plans for that month yet. Save the grid first.";
            return RedirectToAction(nameof(Index), new { year, month, studentId });
        }

        var monthText = new DateTime(year, month, 1).ToString("MMMM yyyy");
        using var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var plan in plans)
            {
                var safe = new string((plan.Student?.FullName ?? "Student").Where(c => char.IsLetterOrDigit(c) || c is ' ' or '-' or '_').ToArray()).Trim();
                var name = $"{safe} - {monthText}.pdf";
                if (!used.Add(name)) name = $"{safe} ({plan.Student?.StudentCode}) - {monthText}.pdf";

                var bytes = await pdf.PlanPdfAsync(plan.Id);
                var entry = zip.CreateEntry(name, CompressionLevel.Fastest);
                await using var entryStream = entry.Open();
                await entryStream.WriteAsync(bytes);
            }
        }

        await audit.LogAsync("PlansDownloaded", $"{year}-{month:D2}", $"{plans.Count} PDF(s)");
        return File(stream.ToArray(), "application/zip", $"Monthly-Plans-{year}-{month:D2}.zip");
    }

    // ---------- One plan: view, publish, PDF ----------

    private async Task<(MonthlyPlan? Plan, IActionResult? Denied)> LoadForReadAsync(Guid id)
    {
        var plan = await db.Plans.AsNoTracking().Include(p => p.Student).Include(p => p.Tutor).Include(p => p.Goals).FirstOrDefaultAsync(p => p.Id == id);
        if (plan?.Student is null) return (null, NotFound());
        if (!await me.CanSeeStudentAsync(plan.Student)) return (null, Forbid());
        if (me.IsStudent)
        {
            if (plan.Status != PublishStatus.Published) return (null, NotFound());
            // Only the plan locks when the portal fee is overdue past the grace period. Join class never locks.
            if (FeeRules.StateOf(plan.Student, await settings.GetSystemAsync()) == FeeState.Locked) return (null, RedirectToAction(nameof(Locked)));
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
        return File(bytes, "application/pdf", $"{plan!.Student!.FullName} - {new DateTime(plan.Year, plan.Month, 1):MMMM yyyy}.pdf");
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Publish(Guid id)
    {
        var plan = await db.Plans.Include(p => p.Student).FirstOrDefaultAsync(p => p.Id == id);
        if (plan?.Student is null) return NotFound();
        var allowed = me.Can(Permissions.ManageSchedules) || (me.IsTutor && plan.Student.TutorId == (await me.TutorAsync())?.Id);
        if (!allowed) return Forbid();

        plan.Status = PublishStatus.Published;
        plan.PublishedAtUtc ??= DateTime.UtcNow;
        await db.SaveChangesAsync();
        await audit.LogAsync("PlanPublished", plan.Student.StudentCode, $"{plan.Year}-{plan.Month:D2}");
        TempData["Saved"] = "Plan published.";
        return RedirectToAction(nameof(Show), new { id });
    }

    /// <summary>Every plan written so far, newest first (tutors and staff).</summary>
    public async Task<IActionResult> History(int page = 1)
    {
        if (!me.IsStaff && !me.IsTutor) return Forbid();
        var query = db.Plans.AsNoTracking().Include(p => p.Student).AsQueryable();
        if (me.IsTutor)
        {
            var tutor = await me.TutorAsync();
            query = query.Where(p => p.TutorId == tutor!.Id);
        }
        var (items, info) = await query.OrderByDescending(p => p.Year).ThenByDescending(p => p.Month).ThenBy(p => p.Student!.FullName).PageAsync(page, 20);
        ViewBag.PageInfo = info;
        return View(items);
    }

    public IActionResult Locked() => View();
}
