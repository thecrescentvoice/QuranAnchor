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
using QuranAnchor.Presentation.Services;
using QuranAnchor.Presentation.ViewModels;

namespace QuranAnchor.Presentation.Controllers;

/// <summary>When tutors can teach, and a finder that shows which tutors are free at a given time.</summary>
[Authorize]
public class AvailabilityController(
    QuranAnchorDbContext db,
    ISchedulingService scheduling,
    IAuditLogger audit,
    CurrentUser me) : Controller
{
    // ---------- A tutor's own windows ----------

    public async Task<IActionResult> Mine()
    {
        var tutor = await me.TutorAsync();
        if (tutor is null) return Forbid();
        ViewBag.Zone = tutor.TimeZoneId;
        return View(await db.Availability.AsNoTracking().Where(a => a.TutorId == tutor.Id).OrderBy(a => a.DayOfWeek).ThenBy(a => a.StartTime).ToListAsync());
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Add(DayOfWeek day, TimeOnly start, TimeOnly end, string? note)
    {
        var tutor = await me.TutorAsync();
        if (tutor is null) return Forbid();
        if (end <= start)
        {
            TempData["Warning"] = "The end time must be later than the start time.";
            return RedirectToAction(nameof(Mine));
        }

        db.Availability.Add(new TutorAvailability { TutorId = tutor.Id, DayOfWeek = day, StartTime = start, EndTime = end, Note = note?.Trim() ?? "" });
        await db.SaveChangesAsync();
        await audit.LogAsync("AvailabilityAdded", tutor.TutorCode, $"{day} {start:HH:mm}-{end:HH:mm}");
        TempData["Saved"] = "Availability added.";
        return RedirectToAction(nameof(Mine));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Remove(Guid id)
    {
        var window = await db.Availability.FirstOrDefaultAsync(a => a.Id == id);
        if (window is null) return NotFound();

        var tutor = await me.TutorAsync();
        if (!(tutor is not null && tutor.Id == window.TutorId) && !me.Can(Permissions.ManageSchedules)) return Forbid();

        db.Availability.Remove(window);
        await db.SaveChangesAsync();
        TempData["Saved"] = "Removed.";
        return me.IsTutor ? RedirectToAction(nameof(Mine)) : RedirectToAction(nameof(Index));
    }

    // ---------- Staff: everyone's windows, and "who is free?" ----------

    [Authorize(Policy = Permissions.ManageSchedules)]
    public async Task<IActionResult> Index(DayOfWeek? day, TimeOnly? time, int duration = 30, string zone = "Africa/Lagos")
    {
        duration = Math.Clamp(duration, 15, 120);
        var tutors = await db.Tutors.AsNoTracking().Where(t => t.Status == AccountStatus.Active).OrderBy(t => t.FullName).ToListAsync();
        var windows = await db.Availability.AsNoTracking().ToListAsync();
        ViewBag.Windows = windows.GroupBy(w => w.TutorId).ToDictionary(g => g.Key, g => g.OrderBy(w => w.DayOfWeek).ThenBy(w => w.StartTime).ToList());
        ViewBag.Tutors = tutors;
        ViewBag.Zones = TimeHelper.CommonZones.Select(z => new SelectListItem(z.Replace('_', ' '), z, z == zone)).ToList();
        ViewBag.Day = day; ViewBag.Time = time; ViewBag.Duration = duration; ViewBag.Zone = zone;

        if (day is null || time is null) return View(new List<TutorSlotResult>());
        return View(await FindFreeAsync(tutors, windows, day.Value, time.Value, duration, zone));
    }

    private async Task<List<TutorSlotResult>> FindFreeAsync(List<TutorProfile> tutors, List<TutorAvailability> windows, DayOfWeek day, TimeOnly time, int duration, string zone)
    {
        // The next date, in the chosen time zone, that falls on the chosen weekday.
        var date = TimeHelper.LocalToday(zone);
        while (date.DayOfWeek != day) date = date.AddDays(1);
        var startUtc = TimeHelper.ToUtc(date, time, zone);
        var endUtc = startUtc.AddMinutes(duration);

        var schedules = await db.Schedules.AsNoTracking().Include(s => s.Student)
            .Where(s => s.EffectiveTo == null || s.EffectiveTo >= date).ToListAsync();

        var results = new List<TutorSlotResult>();
        foreach (var tutor in tutors)
        {
            var local = TimeHelper.ToLocal(startUtc, tutor.TimeZoneId);
            var localStart = TimeOnly.FromDateTime(local);
            var localEnd = TimeOnly.FromDateTime(local.AddMinutes(duration));
            var mine = windows.Where(w => w.TutorId == tutor.Id).ToList();

            if (mine.Count == 0) { results.Add(new TutorSlotResult { Tutor = tutor, Status = "Outside", Detail = "No availability set" }); continue; }

            var inside = mine.Any(w => w.DayOfWeek == local.DayOfWeek && w.StartTime <= localStart && localEnd <= w.EndTime && localEnd > localStart);
            if (!inside) { results.Add(new TutorSlotResult { Tutor = tutor, Status = "Outside", Detail = $"Not available then ({local:ddd h:mm tt} for the tutor)" }); continue; }

            string? busyWith = null;
            foreach (var s in schedules.Where(s => s.TutorId == tutor.Id))
            {
                var sDate = DateOnly.FromDateTime(TimeHelper.ToLocal(startUtc, s.TimeZoneId));
                if (sDate.DayOfWeek != s.DayOfWeek) continue;
                var sStart = TimeHelper.ToUtc(sDate, s.LocalStartTime, s.TimeZoneId);
                if (sStart < endUtc && sStart.AddMinutes(s.DurationMinutes) > startUtc) { busyWith = s.Student?.FullName ?? "another student"; break; }
            }
            if (busyWith is null)
            {
                var clash = await scheduling.FindConflictAsync(tutor.Id, startUtc, duration);
                if (clash is not null) busyWith = clash.Student?.FullName ?? "another student";
            }

            results.Add(busyWith is null
                ? new TutorSlotResult { Tutor = tutor, Status = "Free", Detail = $"{local:ddd h:mm tt} for the tutor" }
                : new TutorSlotResult { Tutor = tutor, Status = "Busy", Detail = $"Teaching {busyWith} at that time" });
        }
        return results.OrderBy(r => r.Status == "Free" ? 0 : r.Status == "Busy" ? 1 : 2).ThenBy(r => r.Tutor.FullName).ToList();
    }
}
