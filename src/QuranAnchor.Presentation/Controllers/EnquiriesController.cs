using System.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using QuranAnchor.Application.Abstractions;
using QuranAnchor.Application.Common;
using QuranAnchor.Domain.Constants;
using QuranAnchor.Domain.Enums;
using QuranAnchor.Infrastructure.Persistence;
using QuranAnchor.Presentation.Helpers;
using QuranAnchor.Presentation.Services;

namespace QuranAnchor.Presentation.Controllers;

/// <summary>The waiting list: enquiries from the public form, trial classes, and turning an enquiry into a student.</summary>
[Authorize(Policy = Permissions.ManageEnquiries)]
public class EnquiriesController(
    QuranAnchorDbContext db,
    ISchedulingService scheduling,
    INotificationService notify,
    IEmailSender email,
    IAuditLogger audit,
    CurrentUser me,
    SupervisorDirectory dir) : Controller
{
    public async Task<IActionResult> Index(EnquiryStatus? status, int page = 1)
    {
        var query = db.Enquiries.AsNoTracking().AsQueryable();
        if (status is not null) query = query.Where(e => e.Status == status);
        var (items, info) = await query.OrderBy(e => e.Status == EnquiryStatus.Enrolled || e.Status == EnquiryStatus.Declined).ThenByDescending(e => e.CreatedAtUtc).PageAsync(page);
        ViewBag.PageInfo = info; ViewBag.Status = status;
        ViewBag.NewCount = await db.Enquiries.CountAsync(e => e.Status == EnquiryStatus.New);
        ViewBag.SupervisorNames = (await dir.AllAsync()).ToDictionary(u => u.Id, u => u.FullName);
        return View(items);
    }

    public async Task<IActionResult> Details(Guid id)
    {
        var e = await db.Enquiries.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (e is null) return NotFound();
        ViewBag.Tutors = (await db.Tutors.AsNoTracking().Where(t => t.Status == AccountStatus.Active).OrderBy(t => t.FullName).ToListAsync())
            .Select(t => new SelectListItem(t.FullName, t.Id.ToString())).ToList();
        ViewBag.SupervisorName = e.SupervisorId is null ? "Nobody yet" : await dir.NameAsync(e.SupervisorId);
        ViewBag.CanTake = me.Role == Roles.Supervisor && e.SupervisorId is null;
        if (me.Can(Permissions.AssignSupervisor)) ViewBag.SupervisorOptions = await dir.OptionsAsync();
        if (me.Role == Roles.Supervisor && e.SupervisorId == me.UserId) ViewBag.HandOverOptions = (await dir.OptionsAsync()).Where(o => o.Value != me.UserId.ToString()).ToList();
        return View(e);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Update(Guid id, EnquiryStatus status, string? notes, DateTime? trialWhen, Guid? trialTutorId, string? trialLink)
    {
        var e = await db.Enquiries.FirstOrDefaultAsync(x => x.Id == id);
        if (e is null) return NotFound();

        e.Status = status;
        // A supervisor who works on an enquiry nobody has taken yet takes it.
        if (e.SupervisorId is null && me.Role == Roles.Supervisor) e.SupervisorId = me.UserId;
        e.Notes = notes?.Trim() ?? "";
        e.TrialMeetingLink = trialLink?.Trim() ?? "";

        var message = "Saved.";
        if (trialWhen is not null && trialTutorId is not null)
        {
            // The time is entered in the family's time zone (shown next to the field).
            var startUtc = TimeHelper.ToUtc(DateOnly.FromDateTime(trialWhen.Value), TimeOnly.FromDateTime(trialWhen.Value), e.TimeZoneId);
            var tutor = await db.Tutors.FirstOrDefaultAsync(t => t.Id == trialTutorId);
            var clash = tutor is null ? null : await scheduling.FindConflictAsync(tutor.Id, startUtc, 30);
            if (tutor is null) message = "Choose a tutor for the trial.";
            else if (clash is not null)
            {
                TempData["Warning"] = $"{tutor.FullName} already has a class with {clash.Student?.FullName} at that time. Nothing was changed.";
                return RedirectToAction(nameof(Details), new { id });
            }
            else
            {
                var changed = e.TrialStartUtc != startUtc || e.TrialTutorId != tutor.Id;
                e.TrialStartUtc = startUtc; e.TrialTutorId = tutor.Id;
                if (e.Status is EnquiryStatus.New or EnquiryStatus.Contacted) e.Status = EnquiryStatus.TrialScheduled;
                if (changed)
                {
                    var link = string.IsNullOrWhiteSpace(e.TrialMeetingLink) ? "We will send you the meeting link before the class." : $"Meeting link: <a href=\"{WebUtility.HtmlEncode(e.TrialMeetingLink)}\">{WebUtility.HtmlEncode(e.TrialMeetingLink)}</a>";
                    var parentBody = await notify.WrapAsync("Your trial class",
                        $"Assalamu alaikum {WebUtility.HtmlEncode(e.ParentName)}. The trial class for <strong>{WebUtility.HtmlEncode(e.StudentName)}</strong> is on <strong>{TimeHelper.Format(startUtc, e.TimeZoneId)}</strong> ({TimeHelper.City(e.TimeZoneId)} time) with {WebUtility.HtmlEncode(tutor.FullName)}.", link);
                    await email.SendAsync(e.Email, "Your trial class", parentBody);

                    var tutorBody = await notify.WrapAsync("A trial class was assigned to you",
                        $"Trial class for {WebUtility.HtmlEncode(e.StudentName)} on <strong>{TimeHelper.Format(startUtc, tutor.TimeZoneId)}</strong> ({TimeHelper.City(tutor.TimeZoneId)} time). Level: {e.Level}.", link);
                    await notify.SendToTutorAsync(tutor, "A trial class was assigned to you", tutorBody);
                    message = "Saved. The parent and the tutor were emailed about the trial class.";
                }
            }
        }

        await db.SaveChangesAsync();
        await audit.LogAsync("EnquiryUpdated", e.StudentName, e.Status.ToString());
        TempData["Saved"] = message;
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Take(Guid id)
    {
        var e = await db.Enquiries.FirstOrDefaultAsync(x => x.Id == id);
        if (e is null) return NotFound();
        if (me.Role != Roles.Supervisor) return Forbid();
        if (e.SupervisorId is not null && e.SupervisorId != me.UserId)
        {
            TempData["Warning"] = "Another supervisor already took this enquiry.";
            return RedirectToAction(nameof(Index));
        }

        e.SupervisorId = me.UserId;
        if (e.Status == EnquiryStatus.New) e.Status = EnquiryStatus.Contacted;
        await db.SaveChangesAsync();
        await audit.LogAsync("EnquiryTaken", e.StudentName);
        TempData["Saved"] = "This enquiry is now yours.";
        return RedirectToAction(nameof(Details), new { id });
    }

    /// <summary>A supervisor hands an enquiry they took to another supervisor.</summary>
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> HandOver(Guid id, Guid supervisorId)
    {
        if (me.Role != Roles.Supervisor) return Forbid();
        var e = await db.Enquiries.FirstOrDefaultAsync(x => x.Id == id);
        if (e is null) return NotFound();
        if (e.SupervisorId != me.UserId) return Forbid();

        var target = (await dir.AllAsync()).FirstOrDefault(u => u.Id == supervisorId && u.IsActive);
        if (target is null || supervisorId == me.UserId)
        {
            TempData["Warning"] = "Choose another active supervisor.";
            return RedirectToAction(nameof(Details), new { id });
        }

        e.SupervisorId = supervisorId;
        await db.SaveChangesAsync();
        await audit.LogAsync("EnquiryHandedOver", e.StudentName, $"to {target.FullName}");

        var from = User.FindFirst(AppClaimTypes.FullName)?.Value ?? "A supervisor";
        var body = await notify.WrapAsync("An enquiry was handed over to you",
            $"{WebUtility.HtmlEncode(from)} handed you the enquiry for <strong>{WebUtility.HtmlEncode(e.StudentName)}</strong> ({WebUtility.HtmlEncode(e.Country)}).",
            "Open Enquiries in the portal to follow up.");
        await notify.SendToSupervisorAsync(supervisorId, "An enquiry was handed over to you", body);

        TempData["Saved"] = $"The enquiry was handed over to {target.FullName}.";
        return RedirectToAction(nameof(Index));
    }

    [Authorize(Policy = Permissions.AssignSupervisor)]
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Assign(Guid id, Guid? supervisorId)
    {
        var e = await db.Enquiries.FirstOrDefaultAsync(x => x.Id == id);
        if (e is null) return NotFound();
        e.SupervisorId = supervisorId;
        await db.SaveChangesAsync();
        await audit.LogAsync("EnquiryAssigned", e.StudentName, supervisorId?.ToString() ?? "nobody");
        TempData["Saved"] = "Saved.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid id)
    {
        var e = await db.Enquiries.FirstOrDefaultAsync(x => x.Id == id);
        if (e is null) return NotFound();
        db.Enquiries.Remove(e);
        await db.SaveChangesAsync();
        await audit.LogAsync("EnquiryDeleted", e.StudentName);
        TempData["Saved"] = "Enquiry deleted.";
        return RedirectToAction(nameof(Index));
    }
}
