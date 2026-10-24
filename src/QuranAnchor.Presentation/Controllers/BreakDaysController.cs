using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuranAnchor.Application.Abstractions;
using QuranAnchor.Domain.Constants;
using QuranAnchor.Domain.Entities;
using QuranAnchor.Infrastructure.Persistence;

namespace QuranAnchor.Presentation.Controllers;

/// <summary>Academy-wide breaks such as Eid. No classes are created on these days, and affected classes carry over.</summary>
[Authorize(Policy = Permissions.ManageBreakDays)]
public class BreakDaysController(
    QuranAnchorDbContext db,
    ISchedulingService scheduling,
    IAuditLogger audit) : Controller
{
    public async Task<IActionResult> Index() =>
        View(await db.BreakDays.AsNoTracking().OrderByDescending(b => b.StartDate).Take(60).ToListAsync());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Add(DateOnly startDate, DateOnly endDate, string reason)
    {
        if (endDate < startDate || string.IsNullOrWhiteSpace(reason))
        {
            TempData["Warning"] = "Enter a start date, an end date that is not earlier, and a reason.";
            return RedirectToAction(nameof(Index));
        }

        await scheduling.AddBreakDayAsync(new BreakDay { StartDate = startDate, EndDate = endDate, Reason = reason.Trim() });
        await audit.LogAsync("BreakDayAdded", reason, $"{startDate:yyyy-MM-dd} to {endDate:yyyy-MM-dd}");
        TempData["Saved"] = "Break added. Classes on those days were cancelled and will carry over.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Remove(Guid id)
    {
        await scheduling.RemoveBreakDayAsync(id);
        await audit.LogAsync("BreakDayRemoved", id.ToString());
        TempData["Saved"] = "Break removed. Classes will be created again for those days.";
        return RedirectToAction(nameof(Index));
    }
}
