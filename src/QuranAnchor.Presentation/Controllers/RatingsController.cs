using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuranAnchor.Application.Abstractions;
using QuranAnchor.Application.Common;
using QuranAnchor.Domain.Constants;
using QuranAnchor.Domain.Entities;
using QuranAnchor.Infrastructure.Persistence;
using QuranAnchor.Presentation.Helpers;
using QuranAnchor.Presentation.Services;
using QuranAnchor.Presentation.ViewModels;

namespace QuranAnchor.Presentation.Controllers;

/// <summary>Parents rate their child's tutor once a month. Staff read the results.</summary>
[Authorize]
public class RatingsController(
    QuranAnchorDbContext db,
    IAuditLogger audit,
    CurrentUser me) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Rate()
    {
        var student = await me.StudentAsync();
        if (student is null) return Forbid();
        var today = TimeHelper.LocalToday(student.TimeZoneId);
        var tutor = student.Tutor;
        var existing = tutor is null ? null : await db.TutorRatings.AsNoTracking().FirstOrDefaultAsync(r => r.StudentId == student.Id && r.Year == today.Year && r.Month == today.Month);

        return View(new RatingFormViewModel
        {
            TutorName = tutor?.FullName ?? "", Year = today.Year, Month = today.Month, HasTutor = tutor is not null,
            Stars = existing?.Stars ?? 0, Comment = existing?.Comment ?? "", AlreadyRated = existing is not null
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Rate(int stars, string? comment)
    {
        var student = await me.StudentAsync();
        if (student?.TutorId is null) return Forbid();
        if (stars is < 1 or > 5)
        {
            TempData["Warning"] = "Choose from 1 to 5 stars.";
            return RedirectToAction(nameof(Rate));
        }

        var today = TimeHelper.LocalToday(student.TimeZoneId);
        var rating = await db.TutorRatings.FirstOrDefaultAsync(r => r.StudentId == student.Id && r.Year == today.Year && r.Month == today.Month);
        if (rating is null)
        {
            rating = new TutorRating { StudentId = student.Id, Year = today.Year, Month = today.Month };
            db.TutorRatings.Add(rating);
        }
        rating.TutorId = student.TutorId.Value;
        rating.Stars = stars;
        rating.Comment = (comment ?? "").Trim();
        await db.SaveChangesAsync();
        await audit.LogAsync("TutorRated", student.StudentCode, $"{stars} stars");

        TempData["Saved"] = "Thank you. Your rating was saved. You can change it until the end of the month.";
        return RedirectToAction(nameof(Rate));
    }

    [Authorize(Policy = Permissions.ViewRatings)]
    public async Task<IActionResult> Index(int page = 1)
    {
        var tutors = await db.Tutors.AsNoTracking().OrderBy(t => t.FullName).ToListAsync();
        var all = await db.TutorRatings.AsNoTracking().ToListAsync();
        var cutoff = DateTime.UtcNow.AddMonths(-3);

        ViewBag.Summary = tutors.Select(t =>
        {
            var mine = all.Where(r => r.TutorId == t.Id).ToList();
            var recent = mine.Where(r => r.CreatedAtUtc >= cutoff).ToList();
            return new TutorRatingRow
            {
                Tutor = t, Count = mine.Count,
                AverageAll = mine.Count == 0 ? 0 : Math.Round(mine.Average(r => r.Stars), 1),
                AverageRecent = recent.Count == 0 ? 0 : Math.Round(recent.Average(r => r.Stars), 1)
            };
        }).Where(r => r.Count > 0).OrderByDescending(r => r.AverageRecent).ToList();

        var (items, info) = await db.TutorRatings.AsNoTracking().Include(r => r.Student).Include(r => r.Tutor)
            .OrderByDescending(r => r.CreatedAtUtc).PageAsync(page, 12);
        ViewBag.PageInfo = info;
        return View(items);
    }
}
