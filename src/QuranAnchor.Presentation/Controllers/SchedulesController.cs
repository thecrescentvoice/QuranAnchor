using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuranAnchor.Application.Abstractions;
using QuranAnchor.Application.Common;
using QuranAnchor.Domain.Constants;
using QuranAnchor.Infrastructure.Persistence;
using QuranAnchor.Presentation.Services;

namespace QuranAnchor.Presentation.Controllers;

/// <summary>A student's weekly class days and times. The tutor edits them after agreeing the change with the parent.</summary>
[Authorize]
public class SchedulesController(
    QuranAnchorDbContext db,
    ISchedulingService scheduling,
    IAuditLogger audit,
    CurrentUser me) : Controller
{
    private async Task<bool> MayEditAsync(Guid studentId)
    {
        if (me.Can(Permissions.ManageSchedules)) return true;
        if (!me.IsTutor) return false;
        var tutor = await me.TutorAsync();
        return tutor is not null && await db.Students.AnyAsync(s => s.Id == studentId && s.TutorId == tutor.Id);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Add(Guid studentId, DayOfWeek day, TimeOnly time, int duration, DateOnly? from)
    {
        if (!await MayEditAsync(studentId)) return Forbid();
        var student = await db.Students.AsNoTracking().FirstOrDefaultAsync(s => s.Id == studentId);
        if (student is null) return NotFound();

        var start = from ?? TimeHelper.LocalToday(student.TimeZoneId);
        var result = await scheduling.AddScheduleAsync(studentId, day, time, duration, start);
        await audit.LogAsync("ScheduleAdded", student.StudentCode, $"{day} {time:HH:mm} {duration} min from {start:yyyy-MM-dd}");
        TempData[result.Ok ? "Saved" : "Warning"] = result.Ok ? "Weekly class added. Upcoming classes were created." : result.Error;
        return RedirectToAction("Details", "Students", new { id = studentId });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Change(Guid scheduleId, DayOfWeek day, TimeOnly time, int duration, DateOnly from)
    {
        var slot = await db.Schedules.AsNoTracking().Include(s => s.Student).FirstOrDefaultAsync(s => s.Id == scheduleId);
        if (slot is null) return NotFound();
        if (!await MayEditAsync(slot.StudentId)) return Forbid();

        var result = await scheduling.ChangeScheduleAsync(scheduleId, day, time, duration, from);
        await audit.LogAsync("ScheduleChanged", slot.Student?.StudentCode, $"{slot.DayOfWeek} {slot.LocalStartTime:HH:mm} -> {day} {time:HH:mm}, from {from:yyyy-MM-dd}");
        TempData[result.Ok ? "Saved" : "Warning"] = result.Ok ? "Schedule changed. The parent was emailed." : result.Error;
        return RedirectToAction("Details", "Students", new { id = slot.StudentId });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Remove(Guid scheduleId, DateOnly from)
    {
        var slot = await db.Schedules.AsNoTracking().Include(s => s.Student).FirstOrDefaultAsync(s => s.Id == scheduleId);
        if (slot is null) return NotFound();
        if (!await MayEditAsync(slot.StudentId)) return Forbid();

        await scheduling.RemoveScheduleAsync(scheduleId, from);
        await audit.LogAsync("ScheduleRemoved", slot.Student?.StudentCode, $"{slot.DayOfWeek} {slot.LocalStartTime:HH:mm} from {from:yyyy-MM-dd}");
        TempData["Saved"] = "Weekly class removed from that date.";
        return RedirectToAction("Details", "Students", new { id = slot.StudentId });
    }
}
