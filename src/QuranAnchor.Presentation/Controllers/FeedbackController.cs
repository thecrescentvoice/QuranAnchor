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

/// <summary>The tutor's feedback form after each class.</summary>
[Authorize]
public class FeedbackController(
    QuranAnchorDbContext db,
    IFeedbackService feedback,
    IAuditLogger audit,
    CurrentUser me) : Controller
{
    private async Task<ClassSession?> LoadForTutorAsync(Guid sessionId)
    {
        var s = await db.Sessions.Include(x => x.Student).Include(x => x.Feedback).FirstOrDefaultAsync(x => x.Id == sessionId);
        if (s is null) return null;
        if (me.Can(Permissions.ManageSchedules)) return s;
        return me.IsTutor && (await me.TutorAsync())?.Id == s.TutorId ? s : null;
    }

    [HttpGet]
    public async Task<IActionResult> Form(Guid sessionId)
    {
        var s = await LoadForTutorAsync(sessionId);
        if (s?.Student is null) return NotFound();

        var model = s.Feedback is null ? new FeedbackFormViewModel { SessionId = sessionId, TimeUsedMinutes = s.DurationMinutes } : FeedbackFormViewModel.From(s.Feedback);
        if (s.Feedback is null) model.LearningObjective = await ObjectiveFromPlanAsync(s);
        model.StudentName = s.Student.FullName;
        model.WhenText = TimeHelper.Format(s.StartUtc, s.Student.TimeZoneId);
        return View(model);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Form(FeedbackFormViewModel model)
    {
        var s = await LoadForTutorAsync(model.SessionId);
        if (s?.Student is null) return NotFound();
        model.StudentName = s.Student.FullName;
        model.WhenText = TimeHelper.Format(s.StartUtc, s.Student.TimeZoneId);
        if (!ModelState.IsValid) return View(model);

        var result = await feedback.SubmitAsync(model.ToEntity(), me.UserId);
        if (!result.Ok)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "The feedback could not be saved.");
            return View(model);
        }

        await audit.LogAsync("FeedbackSubmitted", s.Id.ToString());
        TempData["Saved"] = "Feedback saved. You can copy it as a WhatsApp message below.";
        return RedirectToAction(nameof(CopyText), new { sessionId = model.SessionId });
    }

    /// <summary>The same format the academy uses on WhatsApp, ready to copy.</summary>
    [HttpGet]
    public async Task<IActionResult> CopyText(Guid sessionId)
    {
        var s = await LoadForTutorAsync(sessionId);
        if (s is null) return NotFound();
        ViewBag.SessionId = sessionId;
        return View(model: await feedback.BuildWhatsAppTextAsync(sessionId));
    }

    /// <summary>If the monthly plan has a row covering this class number, its content becomes the learning objective.</summary>
    private async Task<string> ObjectiveFromPlanAsync(ClassSession s)
    {
        var plan = await db.Plans.AsNoTracking().Include(p => p.Rows).FirstOrDefaultAsync(p => p.StudentId == s.StudentId && p.Year == s.StartUtc.Year && p.Month == s.StartUtc.Month);
        if (plan is null)
        {
            var local = TimeHelper.ToLocal(s.StartUtc, s.Student!.TimeZoneId);
            plan = await db.Plans.AsNoTracking().Include(p => p.Rows).FirstOrDefaultAsync(p => p.StudentId == s.StudentId && p.Year == local.Year && p.Month == local.Month);
        }
        if (plan is null) return string.Empty;

        var order = await db.Sessions.AsNoTracking().Where(x => x.PackageId == s.PackageId && x.Status != SessionStatus.Rescheduled && x.Status != SessionStatus.Cancelled)
            .OrderBy(x => x.StartUtc).Select(x => x.Id).ToListAsync();
        var number = order.IndexOf(s.Id) + 1;
        var row = plan.Rows.FirstOrDefault(r => number >= r.SessionFrom && number <= r.SessionTo);
        return row?.Content ?? string.Empty;
    }
}
