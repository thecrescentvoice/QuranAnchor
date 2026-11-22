using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuranAnchor.Application.Common;
using QuranAnchor.Domain.Enums;
using QuranAnchor.Infrastructure.Persistence;
using QuranAnchor.Presentation.Services;
using QuranAnchor.Presentation.ViewModels;

namespace QuranAnchor.Presentation.Controllers;

/// <summary>A tutor's own numbers: classes held, feedback written on time and the parents' average rating. Staff can open any tutor's page.</summary>
[Authorize]
public class PerformanceController(QuranAnchorDbContext db, CurrentUser me) : Controller
{
    public async Task<IActionResult> Mine()
    {
        var tutor = await me.TutorAsync();
        if (tutor is null) return Forbid();
        return RedirectToAction(nameof(Tutor), new { id = tutor.Id });
    }

    public async Task<IActionResult> Tutor(Guid id, int months = 6)
    {
        var own = me.IsTutor ? await me.TutorAsync() : null;
        var isOwn = own is not null && own.Id == id;
        if (!isOwn && !me.IsStaff) return Forbid();

        var tutor = await db.Tutors.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id);
        if (tutor is null) return NotFound();

        months = months is 3 or 6 or 12 ? months : 6;
        var tz = tutor.TimeZoneId;
        var today = TimeHelper.LocalToday(tz);
        var first = new DateOnly(today.Year, today.Month, 1).AddMonths(-(months - 1));
        var fromUtc = TimeHelper.ToUtc(first, TimeOnly.MinValue, tz).AddDays(-1);

        var sessions = await db.Sessions.AsNoTracking().Include(s => s.Feedback)
            .Where(s => s.TutorId == id && s.StartUtc >= fromUtc && s.StartUtc <= DateTime.UtcNow).ToListAsync();
        var local = sessions.Select(s => (Session: s, Local: TimeHelper.ToLocal(s.StartUtc, tz))).ToList();

        var model = new PerformanceViewModel { Tutor = tutor, IsOwn = isOwn, Months = months };
        for (var i = 0; i < months; i++)
        {
            var start = first.AddMonths(i);
            var row = new PerformanceMonth { Label = start.ToString("MMM yyyy", CultureInfo.InvariantCulture) };
            foreach (var (s, _) in local.Where(x => x.Local.Year == start.Year && x.Local.Month == start.Month))
            {
                if (s.Status == SessionStatus.Completed)
                {
                    row.Held++;
                    row.MinutesTaught += s.DurationMinutes;
                    if (s.Feedback is { } f)
                    {
                        row.FeedbackWritten++;
                        // On time means within a day of the class ending.
                        if (f.SubmittedAtUtc <= s.EndUtc.AddHours(24)) row.FeedbackOnTime++;
                    }
                }
                else if (s.Status == SessionStatus.NotHeld)
                {
                    if (s.NotHeldReason == NotHeldReason.StudentAbsent || s.NotHeldReason == NotHeldReason.StudentExcused) row.StudentAbsent++;
                    else row.NotHeldOther++;
                }
            }
            model.Rows.Add(row);
        }

        model.Total = new PerformanceMonth
        {
            Label = "Total", Held = model.Rows.Sum(r => r.Held), StudentAbsent = model.Rows.Sum(r => r.StudentAbsent),
            NotHeldOther = model.Rows.Sum(r => r.NotHeldOther), FeedbackWritten = model.Rows.Sum(r => r.FeedbackWritten),
            FeedbackOnTime = model.Rows.Sum(r => r.FeedbackOnTime), MinutesTaught = model.Rows.Sum(r => r.MinutesTaught)
        };
        model.StudentsTaught = sessions.Where(s => s.Status == SessionStatus.Completed).Select(s => s.StudentId).Distinct().Count();
        model.MissingFeedbackNow = await db.Sessions.CountAsync(s => s.TutorId == id && s.Status == SessionStatus.Completed && s.Feedback == null);

        var ratings = await db.TutorRatings.AsNoTracking().Where(r => r.TutorId == id).ToListAsync();
        model.RatingCount = ratings.Count;
        model.RatingAll = ratings.Count == 0 ? null : Math.Round(ratings.Average(r => r.Stars), 1);
        var recent = ratings.Where(r => r.CreatedAtUtc >= DateTime.UtcNow.AddMonths(-3)).ToList();
        model.RatingRecent = recent.Count == 0 ? null : Math.Round(recent.Average(r => r.Stars), 1);

        return View(model);
    }
}
