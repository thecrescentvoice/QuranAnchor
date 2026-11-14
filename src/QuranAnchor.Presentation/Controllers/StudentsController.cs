using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using QuranAnchor.Application.Abstractions;
using QuranAnchor.Application.Common;
using QuranAnchor.Domain.Constants;
using QuranAnchor.Domain.Entities;
using QuranAnchor.Domain.Enums;
using QuranAnchor.Infrastructure.Identity;
using QuranAnchor.Infrastructure.Persistence;
using QuranAnchor.Presentation.Helpers;
using QuranAnchor.Presentation.Services;
using QuranAnchor.Presentation.ViewModels;

namespace QuranAnchor.Presentation.Controllers;

[Authorize]
public class StudentsController(
    QuranAnchorDbContext db,
    UserManager<ApplicationUser> users,
    IAccountService accounts,
    ISchedulingService scheduling,
    ISettingsService settings,
    IAuditLogger audit,
    CurrentUser me,
    SupervisorDirectory dir,
    INotificationService notify) : Controller
{
    private async Task FillLookupsAsync()
    {
        var tutors = await db.Tutors.AsNoTracking().Where(t => t.Status == AccountStatus.Active).OrderBy(t => t.FullName).ToListAsync();
        ViewBag.Tutors = tutors.Select(t => new SelectListItem(t.FullName, t.Id.ToString())).ToList();
        ViewBag.Zones = TimeHelper.CommonZones.Select(z => new SelectListItem(z.Replace('_', ' '), z)).ToList();
        if (me.Can(Permissions.AssignSupervisor)) ViewBag.Supervisors = await dir.OptionsAsync();
    }

    // ---------- List and details ----------

    public async Task<IActionResult> Index(string? q, StudentLevel? level, AccountStatus? status, int page = 1)
    {
        if (!me.IsStaff && !me.IsTutor) return Forbid();

        var query = db.Students.AsNoTracking().Include(s => s.Tutor).AsQueryable();
        if (me.IsTutor)
        {
            var tutor = await me.TutorAsync();
            query = query.Where(s => s.TutorId == tutor!.Id);
        }
        if (!string.IsNullOrWhiteSpace(q)) query = query.Where(s => s.FullName.Contains(q) || s.StudentCode.Contains(q));
        if (level is not null) query = query.Where(s => s.Level == level);
        if (status is not null) query = query.Where(s => s.Status == status);

        ViewBag.Q = q; ViewBag.Level = level; ViewBag.Status = status;
        var (items, info) = await query.OrderBy(s => s.FullName).PageAsync(page);
        ViewBag.PageInfo = info;
        return View(items);
    }

    public async Task<IActionResult> Details(Guid id)
    {
        var student = await db.Students.Include(s => s.Tutor).FirstOrDefaultAsync(s => s.Id == id);
        if (student is null) return NotFound();
        if (!await me.CanSeeStudentAsync(student)) return Forbid();
        ViewBag.SupervisorName = await dir.NameAsync(student.SupervisorId);
        if (me.Role == Roles.Supervisor)
        {
            if (student.TutorId is null) ViewBag.HandOverOptions = (await dir.OptionsAsync()).Where(o => o.Value != me.UserId.ToString()).ToList();
            else ViewBag.HandOverHint = "A student moves together with their tutor. To hand this student over, hand over the tutor (all their students move), or ask an admin.";
        }

        var today = TimeHelper.LocalToday(student.TimeZoneId);
        var package = await db.Packages.AsNoTracking().FirstOrDefaultAsync(p => p.StudentId == id && p.Year == today.Year && p.Month == today.Month);
        var model = new StudentDetailsViewModel
        {
            Student = student,
            Package = package,
            Usage = package is null ? null : await scheduling.UsageAsync(package.Id),
            Fee = FeeRules.StateOf(student, await settings.GetSystemAsync()),
            Slots = await db.Schedules.AsNoTracking().Where(s => s.StudentId == id && (s.EffectiveTo == null || s.EffectiveTo >= today)).OrderBy(s => s.DayOfWeek).ThenBy(s => s.LocalStartTime).ToListAsync(),
            Upcoming = await db.Sessions.AsNoTracking().Where(s => s.StudentId == id && s.Status == SessionStatus.Scheduled && s.StartUtc > DateTime.UtcNow.AddHours(-2)).OrderBy(s => s.StartUtc).Take(10).ToListAsync(),
            Recent = await db.Sessions.AsNoTracking().Where(s => s.StudentId == id && (s.Status == SessionStatus.Completed || s.Status == SessionStatus.NotHeld)).OrderByDescending(s => s.StartUtc).Take(10).ToListAsync(),
            Plans = await db.Plans.AsNoTracking().Where(p => p.StudentId == id).OrderByDescending(p => p.Year).ThenByDescending(p => p.Month).Take(6).ToListAsync(),
            Reports = await db.Reports.AsNoTracking().Where(r => r.StudentId == id).OrderByDescending(r => r.Year).ThenByDescending(r => r.Month).Take(6).ToListAsync(),
            TempPassword = TempData["TempPassword"] as string
        };
        return View(model);
    }

    // ---------- Create ----------

    [Authorize(Policy = Permissions.CreateAccounts)]
    [HttpGet]
    public async Task<IActionResult> Create(Guid? enquiryId)
    {
        await FillLookupsAsync();
        var model = new StudentFormViewModel();
        if (enquiryId is not null && await db.Enquiries.AsNoTracking().FirstOrDefaultAsync(e => e.Id == enquiryId) is { } enquiry)
        {
            model.EnquiryId = enquiry.Id; model.FullName = enquiry.StudentName; model.ParentName = enquiry.ParentName;
            model.ParentEmail = enquiry.Email; model.Phone = enquiry.Phone; model.Country = enquiry.Country;
            model.TimeZoneId = enquiry.TimeZoneId; model.Level = enquiry.Level; model.SupervisorId = enquiry.SupervisorId;
        }
        return View(model);
    }

    [Authorize(Policy = Permissions.CreateAccounts)]
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(StudentFormViewModel model)
    {
        var slots = new List<(DayOfWeek, TimeOnly, int)>();
        foreach (var s in model.Slots.Where(x => x.Day is not null || x.Time is not null || x.Duration is not null))
        {
            if (s.Day is null || s.Time is null || s.Duration is null || s.Duration < 15 || s.Duration > 120)
            {
                ModelState.AddModelError(string.Empty, "Each class slot needs a day, a time and a length between 15 and 120 minutes.");
                break;
            }
            slots.Add((s.Day.Value, s.Time.Value, s.Duration.Value));
        }

        if (!ModelState.IsValid)
        {
            await FillLookupsAsync();
            return View(model);
        }

        var result = await accounts.CreateStudentAsync(new CreateStudentRequest(
            model.FullName, model.ParentName, model.ParentEmail, model.Phone, model.Country, model.TimeZoneId,
            model.Level, model.TutorId, model.SessionsPerMonth, model.SessionsThisMonth, model.MeetingLink, slots,
            me.Role == Roles.Supervisor ? me.UserId : model.SupervisorId));

        if (!result.Ok)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "The account could not be created.");
            await FillLookupsAsync();
            return View(model);
        }

        await audit.LogAsync("StudentCreated", result.Code, model.FullName);
        TempData["TempPassword"] = result.TempPassword;
        if (result.Error is not null) TempData["Warning"] = result.Error;
        var created = await db.Students.FirstAsync(s => s.StudentCode == result.Code);
        created.ClassFeeAmount = model.ClassFeeAmount; created.ClassFeeCurrency = model.ClassFeeCurrency;
        if (model.EnquiryId is not null && await db.Enquiries.FirstOrDefaultAsync(e => e.Id == model.EnquiryId) is { } fromEnquiry)
        {
            fromEnquiry.Status = EnquiryStatus.Enrolled;
            fromEnquiry.SupervisorId ??= created.SupervisorId;
        }
        await db.SaveChangesAsync();
        TempData["Saved"] = $"Account created. Student ID: {result.Code}. The login details were emailed to the parent.";
        return RedirectToAction(nameof(Details), new { id = created.Id });
    }

    // ---------- Change one student's tutor ----------

    [Authorize(Policy = Permissions.ReplaceTutor)]
    [HttpGet]
    public async Task<IActionResult> ChangeTutor(Guid id)
    {
        var student = await db.Students.AsNoTracking().Include(s => s.Tutor).FirstOrDefaultAsync(s => s.Id == id);
        if (student is null) return NotFound();
        ViewBag.Tutors = (await db.Tutors.AsNoTracking().Where(t => t.Status == AccountStatus.Active && t.Id != student.TutorId).OrderBy(t => t.FullName).ToListAsync())
            .Select(t => new SelectListItem(t.FullName, t.Id.ToString())).ToList();
        return View(student);
    }

    [Authorize(Policy = Permissions.ReplaceTutor)]
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangeTutor(Guid id, Guid newTutorId)
    {
        var student = await db.Students.Include(s => s.Tutor).FirstOrDefaultAsync(s => s.Id == id);
        if (student is null) return NotFound();
        var previous = student.Tutor;

        var result = await accounts.ChangeStudentTutorAsync(id, newTutorId);
        if (!result.Ok)
        {
            TempData["Warning"] = result.Error;
            return RedirectToAction(nameof(ChangeTutor), new { id });
        }

        var incoming = await db.Tutors.AsNoTracking().FirstAsync(t => t.Id == newTutorId);
        await audit.LogAsync("StudentTutorChanged", student.StudentCode, $"{previous?.FullName ?? "none"} to {incoming.FullName}");

        var parentBody = await notify.WrapAsync("Your child's tutor has changed",
            $"Assalamu alaikum. From now on <strong>{System.Net.WebUtility.HtmlEncode(student.FullName)}</strong> will be taught by <strong>{System.Net.WebUtility.HtmlEncode(incoming.FullName)}</strong>.",
            "Your class days and times stay the same. If anything looks wrong, please use the Report an issue button in the portal.");
        await notify.SendToParentAsync(student, "Your child's tutor has changed", parentBody);

        var newBody = await notify.WrapAsync("A student was assigned to you",
            $"<strong>{System.Net.WebUtility.HtmlEncode(student.FullName)}</strong> ({student.StudentCode}) is now your student. Level: {student.Level}.",
            "Their weekly class times and upcoming classes were moved to you. Open My students in the portal for the details.");
        await notify.SendToTutorAsync(incoming, "A student was assigned to you", newBody);

        if (previous is not null)
        {
            var oldBody = await notify.WrapAsync("A student was moved to another tutor",
                $"<strong>{System.Net.WebUtility.HtmlEncode(student.FullName)}</strong> is now taught by another tutor. Their upcoming classes were moved. Thank you for your teaching.");
            await notify.SendToTutorAsync(previous, "A student was moved to another tutor", oldBody);
        }

        TempData["Saved"] = $"{student.FullName} now has {incoming.FullName} as their tutor. Their weekly times and upcoming classes moved too.";
        TempData["Warning"] = "Next step: replace the Meet link below, so the previous tutor can no longer join the class.";
        return RedirectToAction(nameof(Details), new { id });
    }

    /// <summary>A supervisor hands a student who has no tutor yet to another supervisor.</summary>
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> HandOver(Guid id, Guid supervisorId)
    {
        if (me.Role != Roles.Supervisor) return Forbid();
        var student = await db.Students.FirstOrDefaultAsync(s => s.Id == id);   // a supervisor can only load their own students
        if (student is null) return NotFound();
        if (student.TutorId is not null)
        {
            TempData["Warning"] = "A student moves together with their tutor. Hand over the tutor instead, or ask an admin.";
            return RedirectToAction(nameof(Details), new { id });
        }

        var target = (await dir.AllAsync()).FirstOrDefault(u => u.Id == supervisorId && u.IsActive);
        if (target is null || supervisorId == me.UserId)
        {
            TempData["Warning"] = "Choose another active supervisor.";
            return RedirectToAction(nameof(Details), new { id });
        }

        student.SupervisorId = supervisorId;
        await db.SaveChangesAsync();
        await audit.LogAsync("StudentHandedOver", student.StudentCode, $"to {target.FullName}");

        var from = User.FindFirst(AppClaimTypes.FullName)?.Value ?? "A supervisor";
        var body = await notify.WrapAsync("A student was handed over to you",
            $"{System.Net.WebUtility.HtmlEncode(from)} handed <strong>{System.Net.WebUtility.HtmlEncode(student.FullName)}</strong> ({student.StudentCode}) over to you.",
            "You can now see them in the portal.");
        await notify.SendToSupervisorAsync(supervisorId, "A student was handed over to you", body);

        TempData["Saved"] = $"{student.FullName} was handed over to {target.FullName}.";
        return RedirectToAction(nameof(Index));
    }

    // ---------- Edit ----------

    [Authorize(Policy = Permissions.EditAccounts)]
    [HttpGet]
    public async Task<IActionResult> Edit(Guid id)
    {
        var s = await db.Students.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (s is null) return NotFound();
        await FillLookupsAsync();
        return View(new StudentFormViewModel
        {
            Id = s.Id, FullName = s.FullName, ParentName = s.ParentName, ParentEmail = s.ParentEmail, Phone = s.Phone,
            Country = s.Country, TimeZoneId = s.TimeZoneId, Level = s.Level, TutorId = s.TutorId,
            SessionsPerMonth = s.SessionsPerMonth, MeetingLink = s.MeetingLink,
            ClassFeeAmount = s.ClassFeeAmount, ClassFeeCurrency = s.ClassFeeCurrency, SupervisorId = s.SupervisorId
        });
    }

    [Authorize(Policy = Permissions.EditAccounts)]
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Guid id, StudentFormViewModel model)
    {
        ModelState.Remove(nameof(model.TutorId));   // the tutor is changed with Replace tutor
        ModelState.Remove(nameof(model.SessionsThisMonth));
        if (!ModelState.IsValid) { await FillLookupsAsync(); return View(model); }

        var s = await db.Students.FirstOrDefaultAsync(x => x.Id == id);
        if (s is null) return NotFound();

        var oldLevel = s.Level;
        s.FullName = model.FullName.Trim(); s.ParentName = model.ParentName.Trim(); s.ParentEmail = model.ParentEmail.Trim();
        s.Phone = model.Phone?.Trim() ?? ""; s.Country = model.Country?.Trim() ?? ""; s.TimeZoneId = model.TimeZoneId;
        s.Level = model.Level; s.SessionsPerMonth = model.SessionsPerMonth;
        s.ClassFeeAmount = model.ClassFeeAmount; s.ClassFeeCurrency = model.ClassFeeCurrency;
        if (me.Can(Permissions.AssignSupervisor))
        {
            // A student stays with their tutor's supervisor. To move them, change the tutor's supervisor.
            var tutorSupervisor = s.TutorId is null ? null : (await db.Tutors.FirstOrDefaultAsync(t => t.Id == s.TutorId))?.SupervisorId;
            if (tutorSupervisor is not null && model.SupervisorId != tutorSupervisor)
                TempData["Warning"] = "This student follows their tutor's supervisor. To move them, change the tutor's supervisor, or replace the tutor.";
            else
                s.SupervisorId = model.SupervisorId;
        }
        if (s.MeetingLink != (model.MeetingLink ?? "").Trim())
        {
            s.MeetingLink = (model.MeetingLink ?? "").Trim();
            s.MeetingLinkChangedAtUtc = DateTime.UtcNow;
        }
        await db.SaveChangesAsync();

        var user = await users.FindByIdAsync(s.UserId.ToString());
        if (user is not null)
        {
            user.FullName = s.FullName;
            await users.UpdateAsync(user);
            if (!string.Equals(user.Email, s.ParentEmail, StringComparison.OrdinalIgnoreCase)) await users.SetEmailAsync(user, s.ParentEmail);
        }

        await audit.LogAsync("StudentUpdated", s.StudentCode, oldLevel != s.Level ? $"Level {oldLevel} -> {s.Level}" : null);
        TempData["Saved"] = "Saved. A new sessions-per-month number applies from next month.";
        return RedirectToAction(nameof(Details), new { id });
    }

    // ---------- Account actions ----------

    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = Permissions.DeactivateAccounts)]
    public Task<IActionResult> Deactivate(Guid id) => SetActive(id, false);

    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = Permissions.ReactivateAccounts)]
    public Task<IActionResult> Reactivate(Guid id) => SetActive(id, true);

    private async Task<IActionResult> SetActive(Guid id, bool active)
    {
        var s = await db.Students.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (s is null) return NotFound();
        var result = await accounts.SetActiveAsync(s.UserId, active, me.Role);
        await audit.LogAsync(active ? "StudentReactivated" : "StudentDeactivated", s.StudentCode);
        TempData[result.Ok ? "Saved" : "Warning"] = result.Ok ? (active ? "Account reactivated." : "Account deactivated. The history is kept.") : result.Error;
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = Permissions.DeleteAccounts)]
    public async Task<IActionResult> Delete(Guid id)
    {
        var s = await db.Students.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (s is null) return NotFound();
        var result = await accounts.DeletePermanentlyAsync(s.UserId);
        await audit.LogAsync("StudentDeleted", s.StudentCode, result.Ok ? "Permanent delete" : result.Error);
        if (!result.Ok) { TempData["Warning"] = result.Error; return RedirectToAction(nameof(Details), new { id }); }
        TempData["Saved"] = "The account and all its records were permanently deleted.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = Permissions.EditAccounts)]
    public async Task<IActionResult> Pause(Guid id, DateOnly? until)
    {
        await scheduling.SetOnHoldAsync(id, until);
        await audit.LogAsync("StudentPaused", id.ToString(), until?.ToString("yyyy-MM-dd"));
        TempData["Saved"] = "The student is on hold. Future classes were cancelled and fee reminders stop.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = Permissions.EditAccounts)]
    public async Task<IActionResult> Resume(Guid id)
    {
        await scheduling.ResumeAsync(id);
        await audit.LogAsync("StudentResumed", id.ToString());
        TempData["Saved"] = "The student is active again and classes were created.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = Permissions.RotateMeetLink)]
    public async Task<IActionResult> RotateLink(Guid id, string newLink)
    {
        var result = await accounts.RotateMeetLinkAsync(id, newLink);
        await audit.LogAsync("MeetLinkChanged", id.ToString(), result.Ok ? "Link replaced" : result.Error);
        TempData[result.Ok ? "Saved" : "Warning"] = result.Ok ? "The Meet link was replaced." : result.Error;
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = Permissions.EditAccounts)]
    public async Task<IActionResult> ResetPassword(Guid id)
    {
        var s = await db.Students.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (s is null) return NotFound();
        TempData["TempPassword"] = await accounts.ResetTempPasswordAsync(s.UserId);
        await audit.LogAsync("TempPasswordIssued", s.StudentCode);
        TempData["Saved"] = "A new temporary password was made. Give it to the parent; they must change it at the next sign-in.";
        return RedirectToAction(nameof(Details), new { id });
    }
}
