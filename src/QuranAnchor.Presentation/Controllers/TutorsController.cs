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
using QuranAnchor.Presentation.ViewModels;

namespace QuranAnchor.Presentation.Controllers;

[Authorize(Policy = Permissions.ViewAllStudents)]
public class TutorsController(
    QuranAnchorDbContext db,
    IAccountService accounts,
    IReportService reports,
    IAuditLogger audit,
    CurrentUser me,
    SupervisorDirectory dir,
    INotificationService notify) : Controller
{
    private void FillZones() =>
        ViewBag.Zones = TimeHelper.CommonZones.Select(z => new SelectListItem(z.Replace('_', ' '), z)).ToList();

    private async Task FillSupervisorsAsync()
    {
        if (me.Can(Permissions.AssignSupervisor)) ViewBag.Supervisors = await dir.OptionsAsync();
    }

    public async Task<IActionResult> Index(int page = 1)
    {
        var (tutors, info) = await db.Tutors.AsNoTracking().OrderBy(t => t.FullName).PageAsync(page);
        ViewBag.PageInfo = info;
        ViewBag.Counts = await db.Students.GroupBy(s => s.TutorId).Select(g => new { g.Key, N = g.Count() }).ToDictionaryAsync(x => x.Key ?? Guid.Empty, x => x.N);
        return View(tutors);
    }

    public async Task<IActionResult> Details(Guid id)
    {
        var tutor = await db.Tutors.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id);
        if (tutor is null) return NotFound();
        var now = DateTime.UtcNow;
        ViewBag.Students = await db.Students.AsNoTracking().Where(s => s.TutorId == id).OrderBy(s => s.FullName).ToListAsync();
        ViewBag.Summary = (await reports.TutorSummariesAsync(now.Year, now.Month, id)).FirstOrDefault();
        ViewBag.SupervisorName = await dir.NameAsync(tutor.SupervisorId);
        await FillSupervisorsAsync();
        if (me.Role == Roles.Supervisor) ViewBag.HandOverOptions = (await dir.OptionsAsync()).Where(o => o.Value != me.UserId.ToString()).ToList();
        return View(tutor);
    }

    [Authorize(Policy = Permissions.CreateAccounts)]
    [HttpGet]
    public async Task<IActionResult> Create() { FillZones(); await FillSupervisorsAsync(); return View(new TutorFormViewModel()); }

    [Authorize(Policy = Permissions.CreateAccounts)]
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(TutorFormViewModel model)
    {
        if (!ModelState.IsValid) { FillZones(); await FillSupervisorsAsync(); return View(model); }

        // A supervisor's new tutor is theirs. An admin chooses the supervisor.
        var supervisorId = me.Role == Roles.Supervisor ? me.UserId : model.SupervisorId;
        var result = await accounts.CreateTutorAsync(model.FullName, model.Email, model.Phone, model.TimeZoneId, supervisorId);
        if (!result.Ok)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "The account could not be created.");
            FillZones();
            await FillSupervisorsAsync();
            return View(model);
        }

        await audit.LogAsync("TutorCreated", result.Code, model.Email);
        TempData["Saved"] = $"Tutor account created ({result.Code}). Temporary password: {result.TempPassword}. It was also emailed to {model.Email}.";
        var tutor = await db.Tutors.AsNoTracking().FirstAsync(t => t.TutorCode == result.Code);
        return RedirectToAction(nameof(Details), new { id = tutor.Id });
    }

    /// <summary>A supervisor hands one of their tutors, and all that tutor's students, to another supervisor.</summary>
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> HandOver(Guid id, Guid supervisorId)
    {
        if (me.Role != Roles.Supervisor) return Forbid();
        var tutor = await db.Tutors.FirstOrDefaultAsync(t => t.Id == id);   // a supervisor can only load their own tutors
        if (tutor is null) return NotFound();

        var target = (await dir.AllAsync()).FirstOrDefault(u => u.Id == supervisorId && u.IsActive);
        if (target is null || supervisorId == me.UserId)
        {
            TempData["Warning"] = "Choose another active supervisor.";
            return RedirectToAction(nameof(Details), new { id });
        }

        var students = await db.Students.Where(s => s.TutorId == id).ToListAsync();
        tutor.SupervisorId = supervisorId;
        foreach (var student in students) student.SupervisorId = supervisorId;
        await db.SaveChangesAsync();
        await audit.LogAsync("TutorHandedOver", tutor.TutorCode, $"to {target.FullName}, {students.Count} student(s)");

        var from = User.FindFirst(AppClaimTypes.FullName)?.Value ?? "A supervisor";
        var body = await notify.WrapAsync("People handed over to you",
            $"{System.Net.WebUtility.HtmlEncode(from)} handed <strong>{System.Net.WebUtility.HtmlEncode(tutor.FullName)}</strong> and {students.Count} student(s) over to you.",
            "You can now see them in the portal.");
        await notify.SendToSupervisorAsync(supervisorId, "People handed over to you", body);

        TempData["Saved"] = $"{tutor.FullName} and {students.Count} student(s) were handed over to {target.FullName}.";
        return RedirectToAction(nameof(Index));
    }

    [Authorize(Policy = Permissions.AssignSupervisor)]
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AssignSupervisor(Guid id, Guid? supervisorId)
    {
        var tutor = await db.Tutors.FirstOrDefaultAsync(t => t.Id == id);
        if (tutor is null) return NotFound();

        tutor.SupervisorId = supervisorId;
        // Their students move with them.
        foreach (var student in await db.Students.Where(s => s.TutorId == id).ToListAsync()) student.SupervisorId = supervisorId;
        await db.SaveChangesAsync();
        await audit.LogAsync("TutorSupervisorChanged", tutor.TutorCode, supervisorId?.ToString() ?? "none");

        TempData["Saved"] = "Saved. The tutor's students now follow this supervisor.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = Permissions.DeactivateAccounts)]
    public Task<IActionResult> Deactivate(Guid id) => SetActive(id, false);

    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = Permissions.ReactivateAccounts)]
    public Task<IActionResult> Reactivate(Guid id) => SetActive(id, true);

    private async Task<IActionResult> SetActive(Guid id, bool active)
    {
        var t = await db.Tutors.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (t is null) return NotFound();
        var result = await accounts.SetActiveAsync(t.UserId, active, me.Role);
        await audit.LogAsync(active ? "TutorReactivated" : "TutorDeactivated", t.TutorCode);
        TempData[result.Ok ? "Saved" : "Warning"] = result.Ok ? (active ? "Tutor reactivated." : "Tutor deactivated. Their history is kept.") : result.Error;
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = Permissions.DeleteAccounts)]
    public async Task<IActionResult> Delete(Guid id)
    {
        var t = await db.Tutors.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (t is null) return NotFound();
        var result = await accounts.DeletePermanentlyAsync(t.UserId);
        await audit.LogAsync("TutorDeleted", t.TutorCode, result.Ok ? "Permanent delete" : result.Error);
        if (!result.Ok) { TempData["Warning"] = result.Error; return RedirectToAction(nameof(Details), new { id }); }
        TempData["Saved"] = "The tutor account was permanently deleted.";
        return RedirectToAction(nameof(Index));
    }

    // ---------- Replace tutor ----------

    [Authorize(Policy = Permissions.ReplaceTutor)]
    [HttpGet]
    public async Task<IActionResult> Replace(Guid id)
    {
        var leaving = await db.Tutors.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id);
        if (leaving is null) return NotFound();
        ViewBag.Others = await db.Tutors.AsNoTracking().Where(t => t.Id != id && t.Status == AccountStatus.Active).OrderBy(t => t.FullName)
            .Select(t => new SelectListItem(t.FullName, t.Id.ToString())).ToListAsync();
        ViewBag.Students = await db.Students.AsNoTracking().Where(s => s.TutorId == id).OrderBy(s => s.FullName).ToListAsync();
        return View(leaving);
    }

    [Authorize(Policy = Permissions.ReplaceTutor)]
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Replace(Guid id, Guid newTutorId)
    {
        var students = await db.Students.AsNoTracking().Where(s => s.TutorId == id).OrderBy(s => s.FullName).ToListAsync();
        var result = await accounts.ReplaceTutorAsync(id, newTutorId);
        if (!result.Ok)
        {
            TempData["Warning"] = result.Error;
            return RedirectToAction(nameof(Replace), new { id });
        }

        await audit.LogAsync("TutorReplaced", id.ToString(), $"New tutor {newTutorId}; {students.Count} student(s) moved");
        TempData["Saved"] = "Done. The old tutor can no longer sign in, and their students and upcoming classes moved to the new tutor.";
        TempData["RotateChecklist"] = string.Join("|", students.Select(s => $"{s.Id}~{s.FullName}"));
        return RedirectToAction(nameof(Details), new { id = newTutorId });
    }
}
