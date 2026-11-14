using System.Net;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
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

/// <summary>Meetings for tutors. Staff create them; each invited tutor sees the time in their own time zone.</summary>
[Authorize]
public class MeetingsController(
    QuranAnchorDbContext db,
    INotificationService notify,
    IAuditLogger audit,
    CurrentUser me) : Controller
{
    private bool IsManager => me.Can(Permissions.ManageMeetings);

    private async Task<(TutorProfile? Tutor, string Zone)> ViewerAsync()
    {
        var tutor = me.IsTutor ? await me.TutorAsync() : null;
        return (tutor, tutor?.TimeZoneId ?? "Africa/Lagos");
    }

    private static bool IsInvited(Meeting meeting, TutorProfile? tutor) =>
        tutor is not null && ((meeting.AllTutors && (meeting.SupervisorId == null || meeting.SupervisorId == tutor.SupervisorId)) || meeting.Invitees.Any(i => i.TutorId == tutor.Id));

    private async Task FillTutorsAsync()
    {
        ViewBag.Tutors = await db.Tutors.AsNoTracking().Where(t => t.Status == AccountStatus.Active).OrderBy(t => t.FullName).ToListAsync();
        ViewBag.Zones = TimeHelper.CommonZones.Select(z => new SelectListItem(z.Replace('_', ' '), z)).ToList();
    }

    // ---------- List ----------

    public async Task<IActionResult> Index(int page = 1)
    {
        var (tutor, zone) = await ViewerAsync();
        if (!IsManager && tutor is null) return Forbid();

        var query = db.Meetings.AsNoTracking().Include(m => m.Invitees).AsQueryable();
        if (!IsManager) query = query.Where(m => (m.AllTutors && (m.SupervisorId == null || m.SupervisorId == tutor!.SupervisorId)) || m.Invitees.Any(i => i.TutorId == tutor!.Id));

        var now = DateTime.UtcNow;
        ViewBag.Upcoming = await query.Where(m => !m.IsCancelled && m.StartUtc >= now.AddHours(-2)).OrderBy(m => m.StartUtc).ToListAsync();
        var (past, info) = await query.Where(m => m.IsCancelled || m.StartUtc < now.AddHours(-2)).OrderByDescending(m => m.StartUtc).PageAsync(page, 10);
        ViewBag.PageInfo = info; ViewBag.Zone = zone; ViewBag.Manager = IsManager;
        return View(past);
    }

    public async Task<IActionResult> Details(Guid id)
    {
        var meeting = await db.Meetings.AsNoTracking().Include(m => m.Invitees).ThenInclude(i => i.Tutor).FirstOrDefaultAsync(m => m.Id == id);
        if (meeting is null) return NotFound();
        var (tutor, zone) = await ViewerAsync();
        if (!IsManager && !IsInvited(meeting, tutor)) return NotFound();

        ViewBag.Zone = zone; ViewBag.Manager = IsManager;
        if (IsManager && meeting.AllTutors)
            ViewBag.Everyone = await db.Tutors.AsNoTracking().Where(t => t.Status == AccountStatus.Active && (meeting.SupervisorId == null || t.SupervisorId == meeting.SupervisorId)).OrderBy(t => t.FullName).ToListAsync();
        return View(meeting);
    }

    // ---------- Add to calendar ----------

    public async Task<IActionResult> Ics(Guid id)
    {
        var meeting = await db.Meetings.AsNoTracking().Include(m => m.Invitees).FirstOrDefaultAsync(m => m.Id == id);
        if (meeting is null) return NotFound();
        var (tutor, _) = await ViewerAsync();
        if (!IsManager && !IsInvited(meeting, tutor)) return NotFound();

        static string Esc(string? text) => (text ?? "").Replace("\\", "\\\\").Replace(";", "\\;").Replace(",", "\\,").Replace("\r", "").Replace("\n", "\\n");
        static string Stamp(DateTime utc) => utc.ToUniversalTime().ToString("yyyyMMdd'T'HHmmss'Z'");

        var description = meeting.Agenda + (string.IsNullOrWhiteSpace(meeting.MeetingLink) ? "" : "\n\nJoin: " + meeting.MeetingLink);
        var lines = new List<string>
        {
            "BEGIN:VCALENDAR", "VERSION:2.0", "PRODID:-//Quran Anchor//Meetings//EN", "CALSCALE:GREGORIAN", "METHOD:PUBLISH",
            "BEGIN:VEVENT",
            $"UID:{meeting.Id}@quran-anchor",
            $"DTSTAMP:{Stamp(DateTime.UtcNow)}",
            $"DTSTART:{Stamp(meeting.StartUtc)}",
            $"DTEND:{Stamp(meeting.StartUtc.AddMinutes(meeting.DurationMinutes))}",
            $"SUMMARY:{Esc(meeting.Title)}",
            $"DESCRIPTION:{Esc(description)}"
        };
        if (!string.IsNullOrWhiteSpace(meeting.MeetingLink)) lines.Add($"URL:{Esc(meeting.MeetingLink)}");
        lines.Add("END:VEVENT");
        lines.Add("END:VCALENDAR");

        return File(Encoding.UTF8.GetBytes(string.Join("\r\n", lines) + "\r\n"), "text/calendar", "meeting.ics");
    }

    // ---------- Create ----------

    [Authorize(Policy = Permissions.ManageMeetings)]
    [HttpGet]
    public async Task<IActionResult> Create()
    {
        await FillTutorsAsync();
        return View(new MeetingFormViewModel());
    }

    [Authorize(Policy = Permissions.ManageMeetings)]
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(MeetingFormViewModel model)
    {
        if (!model.AllTutors && model.SelectedTutorIds.Count == 0) ModelState.AddModelError(nameof(model.SelectedTutorIds), "Choose at least one tutor, or invite every tutor.");
        if (!ModelState.IsValid)
        {
            await FillTutorsAsync();
            return View(model);
        }

        var meeting = new Meeting
        {
            Title = model.Title.Trim(), Agenda = model.Agenda?.Trim() ?? "", MeetingLink = model.MeetingLink?.Trim() ?? "",
            DurationMinutes = model.DurationMinutes, TimeZoneId = model.TimeZoneId, AllTutors = model.AllTutors,
            StartUtc = TimeHelper.ToUtc(DateOnly.FromDateTime(model.When!.Value), TimeOnly.FromDateTime(model.When.Value), model.TimeZoneId),
            CreatedBy = User.FindFirst(AppClaimTypes.FullName)?.Value ?? User.Identity?.Name ?? "",
            SupervisorId = me.Role == Roles.Supervisor ? me.UserId : null
        };
        if (!model.AllTutors)
            foreach (var tutorId in model.SelectedTutorIds.Distinct()) meeting.Invitees.Add(new MeetingInvitee { TutorId = tutorId });

        db.Meetings.Add(meeting);
        await db.SaveChangesAsync();
        await audit.LogAsync("MeetingCreated", meeting.Title);
        await EmailInviteesAsync(meeting, $"Meeting: {meeting.Title}", "You are invited to a meeting");

        TempData["Saved"] = "Meeting created and the tutors were emailed.";
        return RedirectToAction(nameof(Details), new { id = meeting.Id });
    }

    // ---------- Edit ----------

    [Authorize(Policy = Permissions.ManageMeetings)]
    [HttpGet]
    public async Task<IActionResult> Edit(Guid id)
    {
        var m = await db.Meetings.AsNoTracking().Include(x => x.Invitees).FirstOrDefaultAsync(x => x.Id == id);
        if (m is null) return NotFound();
        await FillTutorsAsync();
        return View(new MeetingFormViewModel
        {
            Id = m.Id, Title = m.Title, Agenda = m.Agenda, MeetingLink = m.MeetingLink, DurationMinutes = m.DurationMinutes,
            TimeZoneId = m.TimeZoneId, AllTutors = m.AllTutors, SelectedTutorIds = m.Invitees.Select(i => i.TutorId).ToList(),
            When = TimeHelper.ToLocal(m.StartUtc, m.TimeZoneId), Notes = m.Notes
        });
    }

    [Authorize(Policy = Permissions.ManageMeetings)]
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Guid id, MeetingFormViewModel model)
    {
        var meeting = await db.Meetings.Include(m => m.Invitees).FirstOrDefaultAsync(m => m.Id == id);
        if (meeting is null) return NotFound();
        model.Id = id;

        if (!model.AllTutors && model.SelectedTutorIds.Count == 0) ModelState.AddModelError(nameof(model.SelectedTutorIds), "Choose at least one tutor, or invite every tutor.");
        if (!ModelState.IsValid)
        {
            await FillTutorsAsync();
            return View(model);
        }

        var newStart = TimeHelper.ToUtc(DateOnly.FromDateTime(model.When!.Value), TimeOnly.FromDateTime(model.When.Value), model.TimeZoneId);
        var changedForTutors = newStart != meeting.StartUtc || model.MeetingLink?.Trim() != meeting.MeetingLink || model.DurationMinutes != meeting.DurationMinutes;

        meeting.Title = model.Title.Trim(); meeting.Agenda = model.Agenda?.Trim() ?? ""; meeting.MeetingLink = model.MeetingLink?.Trim() ?? "";
        meeting.DurationMinutes = model.DurationMinutes; meeting.TimeZoneId = model.TimeZoneId; meeting.StartUtc = newStart;
        meeting.Notes = model.Notes?.Trim() ?? "";
        meeting.AllTutors = model.AllTutors;

        db.MeetingInvitees.RemoveRange(meeting.Invitees);
        meeting.Invitees = new List<MeetingInvitee>();
        if (!model.AllTutors)
            foreach (var tutorId in model.SelectedTutorIds.Distinct()) meeting.Invitees.Add(new MeetingInvitee { TutorId = tutorId });

        await db.SaveChangesAsync();
        await audit.LogAsync("MeetingEdited", meeting.Title);
        if (changedForTutors && !meeting.IsCancelled) await EmailInviteesAsync(meeting, $"Meeting updated: {meeting.Title}", "A meeting was updated");

        TempData["Saved"] = changedForTutors ? "Saved. The tutors were emailed the new details." : "Saved.";
        return RedirectToAction(nameof(Details), new { id });
    }

    // ---------- Cancel and delete ----------

    [Authorize(Policy = Permissions.ManageMeetings)]
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(Guid id)
    {
        var meeting = await db.Meetings.Include(m => m.Invitees).FirstOrDefaultAsync(m => m.Id == id);
        if (meeting is null) return NotFound();
        if (meeting.IsCancelled) { TempData["Warning"] = "That meeting is already cancelled."; return RedirectToAction(nameof(Details), new { id }); }

        meeting.IsCancelled = true;
        await db.SaveChangesAsync();
        await audit.LogAsync("MeetingCancelled", meeting.Title);
        await EmailInviteesAsync(meeting, $"Meeting cancelled: {meeting.Title}", "A meeting was cancelled", cancelled: true);
        TempData["Saved"] = "Meeting cancelled and the tutors were emailed.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [Authorize(Policy = Permissions.ManageMeetings)]
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid id)
    {
        var meeting = await db.Meetings.FirstOrDefaultAsync(m => m.Id == id);
        if (meeting is null) return NotFound();
        db.Meetings.Remove(meeting);
        await db.SaveChangesAsync();
        await audit.LogAsync("MeetingDeleted", meeting.Title);
        TempData["Saved"] = "Meeting deleted.";
        return RedirectToAction(nameof(Index));
    }

    // ---------- Email ----------

    private async Task EmailInviteesAsync(Meeting meeting, string subject, string heading, bool cancelled = false)
    {
        var tutors = await db.Tutors.Where(t => t.Status == AccountStatus.Active).ToListAsync();
        var invited = meeting.AllTutors
            ? tutors.Where(t => meeting.SupervisorId == null || t.SupervisorId == meeting.SupervisorId).ToList()
            : tutors.Where(t => meeting.Invitees.Any(i => i.TutorId == t.Id)).ToList();

        foreach (var tutor in invited)
        {
            var paragraphs = new List<string>
            {
                $"<strong>{WebUtility.HtmlEncode(meeting.Title)}</strong>",
                cancelled
                    ? $"It was planned for {TimeHelper.Format(meeting.StartUtc, tutor.TimeZoneId)} ({TimeHelper.City(tutor.TimeZoneId)} time) and has been cancelled."
                    : $"When: <strong>{TimeHelper.Format(meeting.StartUtc, tutor.TimeZoneId)}</strong> ({TimeHelper.City(tutor.TimeZoneId)} time), {meeting.DurationMinutes} minutes."
            };
            if (!cancelled)
            {
                if (!string.IsNullOrWhiteSpace(meeting.Agenda)) paragraphs.Add("Agenda:<br />" + Fmt.LinkifyHtml(meeting.Agenda, lineBreaks: true));
                paragraphs.Add(string.IsNullOrWhiteSpace(meeting.MeetingLink)
                    ? "The meeting link will be shared before the meeting."
                    : $"Join: <a href=\"{WebUtility.HtmlEncode(meeting.MeetingLink)}\">{WebUtility.HtmlEncode(meeting.MeetingLink)}</a>");
            }
            var body = await notify.WrapAsync(heading, paragraphs.ToArray());
            await notify.SendToTutorAsync(tutor, subject, body);
        }
    }
}
