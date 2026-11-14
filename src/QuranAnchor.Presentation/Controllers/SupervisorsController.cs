using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuranAnchor.Application.Abstractions;
using QuranAnchor.Domain.Constants;
using QuranAnchor.Infrastructure.Persistence;
using QuranAnchor.Presentation.Services;

namespace QuranAnchor.Presentation.Controllers;

/// <summary>For admins: how many tutors and students each supervisor looks after, and handing over people nobody looks after yet.</summary>
[Authorize(Policy = Permissions.AssignSupervisor)]
public class SupervisorsController(
    QuranAnchorDbContext db,
    SupervisorDirectory dir,
    INotificationService notify,
    IAuditLogger audit) : Controller
{
    public async Task<IActionResult> Index()
    {
        var supervisors = await dir.AllAsync();
        ViewBag.Options = await dir.OptionsAsync();
        ViewBag.Tutors = await db.Tutors.GroupBy(t => t.SupervisorId).Select(g => new { g.Key, N = g.Count() }).ToDictionaryAsync(x => x.Key ?? Guid.Empty, x => x.N);
        ViewBag.Students = await db.Students.GroupBy(s => s.SupervisorId).Select(g => new { g.Key, N = g.Count() }).ToDictionaryAsync(x => x.Key ?? Guid.Empty, x => x.N);
        ViewBag.Enquiries = await db.Enquiries.Where(e => e.SupervisorId != null).GroupBy(e => e.SupervisorId).Select(g => new { g.Key, N = g.Count() }).ToDictionaryAsync(x => x.Key ?? Guid.Empty, x => x.N);
        return View(supervisors);
    }

    /// <summary>Moves everything one supervisor looks after to another supervisor (for example when someone leaves).</summary>
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> MoveAll(Guid fromSupervisorId, Guid toSupervisorId)
    {
        var target = (await dir.AllAsync()).FirstOrDefault(u => u.Id == toSupervisorId && u.IsActive);
        if (target is null || fromSupervisorId == toSupervisorId)
        {
            TempData["Warning"] = "Choose a different, active supervisor to move everything to.";
            return RedirectToAction(nameof(Index));
        }

        var tutors = await db.Tutors.Where(t => t.SupervisorId == fromSupervisorId).ToListAsync();
        var students = await db.Students.Where(s => s.SupervisorId == fromSupervisorId).ToListAsync();
        var enquiries = await db.Enquiries.Where(e => e.SupervisorId == fromSupervisorId).ToListAsync();
        foreach (var t in tutors) t.SupervisorId = toSupervisorId;
        foreach (var s in students) s.SupervisorId = toSupervisorId;
        foreach (var e in enquiries) e.SupervisorId = toSupervisorId;
        await db.SaveChangesAsync();
        await audit.LogAsync("SupervisorPeopleMoved", $"{await dir.NameAsync(fromSupervisorId)} to {target.FullName}", $"{tutors.Count} tutor(s), {students.Count} student(s), {enquiries.Count} enquiry(ies)");

        var body = await notify.WrapAsync("People handed over to you",
            $"{tutors.Count} tutor(s), {students.Count} student(s) and {enquiries.Count} enquiry(ies) were handed over to you.",
            "You can now see them in the portal.");
        await notify.SendToSupervisorAsync(toSupervisorId, "People handed over to you", body);

        TempData["Saved"] = $"Moved {tutors.Count} tutor(s), {students.Count} student(s) and {enquiries.Count} enquiry(ies) to {target.FullName}.";
        return RedirectToAction(nameof(Index));
    }

    /// <summary>Gives every tutor and student with no supervisor to one supervisor. A student whose tutor already has a supervisor follows that tutor.</summary>
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AssignUnassigned(Guid supervisorId)
    {
        var tutors = await db.Tutors.ToListAsync();
        foreach (var tutor in tutors.Where(t => t.SupervisorId is null)) tutor.SupervisorId = supervisorId;
        var supervisorOfTutor = tutors.ToDictionary(t => t.Id, t => t.SupervisorId);

        var students = await db.Students.Where(s => s.SupervisorId == null).ToListAsync();
        foreach (var student in students)
            student.SupervisorId = student.TutorId is { } tid && supervisorOfTutor.TryGetValue(tid, out var fromTutor) && fromTutor is not null ? fromTutor : supervisorId;

        await db.SaveChangesAsync();
        await audit.LogAsync("UnassignedHandedToSupervisor", supervisorId.ToString(), $"{tutors.Count(t => t.SupervisorId == supervisorId)} tutor(s), {students.Count} student(s)");
        TempData["Saved"] = "Done. Everyone who had no supervisor now has one.";
        return RedirectToAction(nameof(Index));
    }
}
