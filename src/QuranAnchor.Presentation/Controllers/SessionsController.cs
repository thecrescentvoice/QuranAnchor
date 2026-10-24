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
public class SessionsController(
    QuranAnchorDbContext db,
    ISchedulingService scheduling,
    IFeedbackService feedback,
    ISettingsService settings,
    IAuditLogger audit,
    CurrentUser me) : Controller
{
    private async Task<ClassSession?> LoadAsync(Guid id)
    {
        var s = await db.Sessions.Include(x => x.Student).Include(x => x.Tutor).Include(x => x.Feedback).FirstOrDefaultAsync(x => x.Id == id);
        if (s is null) return null;
        if (me.IsStaff) return s;
        if (me.IsTutor && (await me.TutorAsync())?.Id == s.TutorId) return s;
        if (me.IsStudent && (await me.StudentAsync())?.Id == s.StudentId) return s;
        return null;
    }

    private async Task<string> ViewerZoneAsync(ClassSession s) =>
        me.IsTutor ? (await me.TutorAsync())?.TimeZoneId ?? s.Student!.TimeZoneId : s.Student!.TimeZoneId;

    // ---------- Calendar ----------

    public async Task<IActionResult> Index(DateOnly? from, int days = 14, Guid? studentId = null)
    {
        var zone = "Africa/Lagos";
        IQueryable<ClassSession> query = db.Sessions.AsNoTracking().Include(s => s.Student).Include(s => s.Tutor);

        if (me.IsStudent)
        {
            var student = await me.StudentAsync();
            if (student is null) return Forbid();
            zone = student.TimeZoneId;
            query = query.Where(s => s.StudentId == student.Id);
        }
        else if (me.IsTutor)
        {
            var tutor = await me.TutorAsync();
            if (tutor is null) return Forbid();
            zone = tutor.TimeZoneId;
            query = query.Where(s => s.TutorId == tutor.Id);
        }
        else if (studentId is not null)
        {
            query = query.Where(s => s.StudentId == studentId);
        }

        days = Math.Clamp(days, 1, 62);
        var start = from ?? TimeHelper.LocalToday(zone);
        var fromUtc = TimeHelper.ToUtc(start, TimeOnly.MinValue, zone);
        var toUtc = fromUtc.AddDays(days);

        var list = await query.Where(s => s.StartUtc >= fromUtc && s.StartUtc < toUtc && s.Status != SessionStatus.Rescheduled).OrderBy(s => s.StartUtc).ToListAsync();
        ViewBag.Zone = zone; ViewBag.From = start; ViewBag.Days = days; ViewBag.StudentId = studentId;
        return View(list);
    }

    // ---------- One class ----------

    public async Task<IActionResult> Details(Guid id)
    {
        var s = await LoadAsync(id);
        if (s?.Student is null || s.Tutor is null) return NotFound();

        var rules = await settings.GetSystemAsync();
        var now = DateTime.UtcNow;
        var package = await db.Packages.AsNoTracking().FirstAsync(p => p.Id == s.PackageId);
        var pending = await db.RescheduleRequests.AsNoTracking().FirstOrDefaultAsync(r => r.SessionId == id && r.Status == RequestStatus.Pending);

        var vm = new SessionDetailsViewModel
        {
            Session = s, Student = s.Student, Tutor = s.Tutor, ViewerZone = await ViewerZoneAsync(s),
            CanJoin = (me.IsStudent || me.IsTutor) && s.Status == SessionStatus.Scheduled && now >= s.StartUtc.AddMinutes(-rules.JoinOpensMinutesBefore) && now <= s.EndUtc,
            CanWriteFeedback = (me.IsTutor || me.Can(Permissions.ManageSchedules)) && s.Status is SessionStatus.Scheduled or SessionStatus.Completed && s.StartUtc <= now.AddMinutes(5),
            CanMarkNotHeld = (me.IsTutor || me.Can(Permissions.ManageSchedules)) && s.Status == SessionStatus.Scheduled && s.StartUtc <= now,
            CanRequestReschedule = (me.IsStudent || me.IsTutor) && s.Status == SessionStatus.Scheduled && s.StartUtc > now && pending is null,
            Pending = pending, PackageTotal = package.Total,
            UsedCount = await db.Sessions.CountAsync(x => x.PackageId == s.PackageId && x.UsesPackageSession && x.StartUtc <= s.StartUtc)
        };
        vm.CanRespond = pending is not null && ((pending.RequestedByTutor && me.IsStudent) || (!pending.RequestedByTutor && me.IsTutor) || me.Can(Permissions.OverrideReschedules));
        return View(vm);
    }

    /// <summary>The Join class button. Opens only around class time, and only for the student and tutor of that class.</summary>
    public async Task<IActionResult> Join(Guid id)
    {
        var s = await LoadAsync(id);
        if (s?.Student is null) return NotFound();
        if (!me.IsStudent && !me.IsTutor) return Forbid();

        var rules = await settings.GetSystemAsync();
        var now = DateTime.UtcNow;
        if (s.Status != SessionStatus.Scheduled || now < s.StartUtc.AddMinutes(-rules.JoinOpensMinutesBefore) || now > s.EndUtc)
        {
            TempData["Warning"] = $"Join opens {rules.JoinOpensMinutesBefore} minutes before the class and closes when it ends.";
            return RedirectToAction(nameof(Details), new { id });
        }
        if (string.IsNullOrWhiteSpace(s.Student.MeetingLink))
        {
            TempData["Warning"] = "No Meet link is set for this student yet. Please tell the supervisor.";
            return RedirectToAction(nameof(Details), new { id });
        }
        return Redirect(s.Student.MeetingLink);
    }

    // ---------- Actions on a class ----------

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> NotHeld(Guid id, NotHeldReason reason, string? note)
    {
        var s = await LoadAsync(id);
        if (s is null) return NotFound();
        if (!me.IsTutor && !me.Can(Permissions.ManageSchedules)) return Forbid();

        var result = await feedback.MarkNotHeldAsync(id, reason, note ?? string.Empty);
        await audit.LogAsync("ClassMarkedNotHeld", id.ToString(), reason.ToString());
        TempData[result.Ok ? "Saved" : "Warning"] = result.Ok ? "Saved." : result.Error;
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AddExtra(Guid studentId, DateTime when, int duration)
    {
        var student = await db.Students.AsNoTracking().FirstOrDefaultAsync(s => s.Id == studentId);
        if (student is null) return NotFound();
        var allowed = me.Can(Permissions.ManageSchedules) || (me.IsTutor && student.TutorId == (await me.TutorAsync())?.Id);
        if (!allowed) return Forbid();

        // The time is entered in the student's time zone (shown next to the field).
        var startUtc = TimeHelper.ToUtc(DateOnly.FromDateTime(when), TimeOnly.FromDateTime(when), student.TimeZoneId);
        var result = await scheduling.AddExtraSessionAsync(studentId, startUtc, duration);
        await audit.LogAsync("ExtraSessionAdded", student.StudentCode, startUtc.ToString("u"));
        TempData[result.Ok ? "Saved" : "Warning"] = result.Ok ? "Extra class added. The parent was emailed." : result.Error;
        return RedirectToAction("Details", "Students", new { id = studentId });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Reschedule(Guid id, DateTime when, string reason)
    {
        var s = await LoadAsync(id);
        if (s?.Student is null) return NotFound();
        if (!me.IsStudent && !me.IsTutor) return Forbid();
        if (string.IsNullOrWhiteSpace(reason)) { TempData["Warning"] = "Please give a short reason."; return RedirectToAction(nameof(Details), new { id }); }

        var zone = await ViewerZoneAsync(s);
        var proposed = TimeHelper.ToUtc(DateOnly.FromDateTime(when), TimeOnly.FromDateTime(when), zone);
        var result = await scheduling.RequestRescheduleAsync(id, me.UserId, me.IsTutor, proposed, reason);
        await audit.LogAsync("RescheduleRequested", id.ToString(), proposed.ToString("u"));
        TempData[result.Ok ? "Saved" : "Warning"] = result.Ok ? "Request sent. The class moves only when the other side accepts." : result.Error;
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Respond(Guid requestId, bool accept)
    {
        var request = await db.RescheduleRequests.AsNoTracking().FirstOrDefaultAsync(r => r.Id == requestId);
        if (request is null) return NotFound();
        var s = await LoadAsync(request.SessionId);
        if (s is null) return Forbid();

        var isOverride = me.Can(Permissions.OverrideReschedules);
        var isOtherSide = (request.RequestedByTutor && me.IsStudent) || (!request.RequestedByTutor && me.IsTutor);
        if (!isOverride && !isOtherSide) return Forbid();

        var result = await scheduling.RespondRescheduleAsync(requestId, me.UserId, accept, isOverride);
        await audit.LogAsync(accept ? "RescheduleAccepted" : "RescheduleDeclined", requestId.ToString(), isOverride ? "Override" : null);
        TempData[result.Ok ? "Saved" : "Warning"] = result.Ok ? (accept ? "The class was moved." : "Request declined.") : result.Error;
        return RedirectToAction(nameof(Details), new { id = request.SessionId });
    }
}
