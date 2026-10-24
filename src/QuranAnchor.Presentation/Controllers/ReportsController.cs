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
public class ReportsController(
    QuranAnchorDbContext db,
    IReportService reports,
    IPdfService pdf,
    IAuditLogger audit,
    CurrentUser me) : Controller
{
    private async Task<bool> CanWriteAsync(StudentProfile s)
    {
        if (me.Can(Permissions.ManageSchedules)) return true;
        return me.IsTutor && s.TutorId == (await me.TutorAsync())?.Id;
    }

    public async Task<IActionResult> Mine()
    {
        var student = await me.StudentAsync();
        return student is null ? Forbid() : RedirectToAction(nameof(Student), new { studentId = student.Id });
    }

    /// <summary>One student's report for one month. Parents see it only after the tutor has published it.</summary>
    public async Task<IActionResult> Student(Guid studentId, int? year, int? month)
    {
        var student = await db.Students.AsNoTracking().FirstOrDefaultAsync(s => s.Id == studentId);
        if (student is null) return NotFound();
        if (!await me.CanSeeStudentAsync(student)) return Forbid();

        var today = TimeHelper.LocalToday(student.TimeZoneId);
        var y = year ?? today.Year; var m = month ?? today.Month;
        var remarks = await db.Reports.AsNoTracking().FirstOrDefaultAsync(r => r.StudentId == studentId && r.Year == y && r.Month == m);
        var canWrite = await CanWriteAsync(student);

        if (me.IsStudent && remarks?.Status != PublishStatus.Published)
        {
            ViewBag.Student = student; ViewBag.Year = y; ViewBag.Month = m;
            return View("NotPublished");
        }

        ViewBag.CanWrite = canWrite;
        ViewBag.Remarks = remarks ?? new MonthlyReport { StudentId = studentId, Year = y, Month = m };
        ViewBag.Student = student;
        return View(await reports.BuildAsync(studentId, y, m));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(ReportRemarksViewModel model)
    {
        var student = await db.Students.FirstOrDefaultAsync(s => s.Id == model.StudentId);
        if (student?.TutorId is null) return NotFound();
        if (!await CanWriteAsync(student)) return Forbid();

        var report = await db.Reports.FirstOrDefaultAsync(r => r.StudentId == model.StudentId && r.Year == model.Year && r.Month == model.Month);
        if (report is null)
        {
            report = new MonthlyReport { StudentId = student.Id, TutorId = student.TutorId.Value, Year = model.Year, Month = model.Month };
            db.Reports.Add(report);
        }
        report.FocusNextMonth = model.FocusNextMonth ?? ""; report.TutorRemark = model.TutorRemark ?? ""; report.ParentNote = model.ParentNote ?? "";
        await db.SaveChangesAsync();
        await audit.LogAsync("ReportSaved", student.StudentCode, $"{model.Year}-{model.Month:D2}");
        TempData["Saved"] = "Saved as a draft.";
        return RedirectToAction(nameof(Student), new { studentId = model.StudentId, year = model.Year, month = model.Month });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Publish(Guid studentId, int year, int month)
    {
        var student = await db.Students.FirstOrDefaultAsync(s => s.Id == studentId);
        if (student?.TutorId is null) return NotFound();
        if (!await CanWriteAsync(student)) return Forbid();

        var report = await db.Reports.FirstOrDefaultAsync(r => r.StudentId == studentId && r.Year == year && r.Month == month);
        if (report is null)
        {
            report = new MonthlyReport { StudentId = studentId, TutorId = student.TutorId.Value, Year = year, Month = month };
            db.Reports.Add(report);
        }
        report.Status = PublishStatus.Published;
        report.PublishedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync();
        await audit.LogAsync("ReportPublished", student.StudentCode, $"{year}-{month:D2}");
        TempData["Saved"] = "Report published. The parent can now see and download it.";
        return RedirectToAction(nameof(Student), new { studentId, year, month });
    }

    public async Task<IActionResult> Pdf(Guid studentId, int year, int month)
    {
        var student = await db.Students.AsNoTracking().FirstOrDefaultAsync(s => s.Id == studentId);
        if (student is null) return NotFound();
        if (!await me.CanSeeStudentAsync(student)) return Forbid();

        if (me.IsStudent)
        {
            var published = await db.Reports.AsNoTracking().AnyAsync(r => r.StudentId == studentId && r.Year == year && r.Month == month && r.Status == PublishStatus.Published);
            if (!published) return NotFound();
        }
        var bytes = await pdf.ReportPdfAsync(studentId, year, month);
        return File(bytes, "application/pdf", $"Monthly-Report-{student.StudentCode}-{year}-{month:D2}.pdf");
    }
}
