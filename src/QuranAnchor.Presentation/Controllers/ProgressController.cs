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

/// <summary>Progress charts for one child: attendance, goals reached and daily tasks, month by month.</summary>
[Authorize]
public class ProgressController(QuranAnchorDbContext db, CurrentUser me) : Controller
{
    /// <summary>For a parent or student: their own child.</summary>
    public async Task<IActionResult> Mine()
    {
        var student = await me.StudentAsync();
        if (student is null) return Forbid();
        return RedirectToAction(nameof(Student), new { id = student.Id });
    }

    public async Task<IActionResult> Student(Guid id, int months = 6)
    {
        var student = await db.Students.AsNoTracking().Include(s => s.Tutor).FirstOrDefaultAsync(s => s.Id == id);
        if (student is null) return NotFound();
        if (!await me.CanSeeStudentAsync(student)) return Forbid();

        months = months is 3 or 6 or 12 ? months : 6;
        var tz = student.TimeZoneId;
        var today = TimeHelper.LocalToday(tz);
        var first = new DateOnly(today.Year, today.Month, 1).AddMonths(-(months - 1));
        var fromUtc = TimeHelper.ToUtc(first, TimeOnly.MinValue, tz).AddDays(-1);

        var sessions = await db.Sessions.AsNoTracking().Include(s => s.Feedback)
            .Where(s => s.StudentId == id && s.StartUtc >= fromUtc && s.StartUtc <= DateTime.UtcNow)
            .OrderBy(s => s.StartUtc).ToListAsync();

        var local = sessions.Select(s => (Session: s, Local: TimeHelper.ToLocal(s.StartUtc, tz))).ToList();
        var model = new ProgressViewModel { Student = student, Months = months };

        for (var i = 0; i < months; i++)
        {
            var start = first.AddMonths(i);
            var row = new ProgressMonth { Label = start.ToString("MMM yyyy", CultureInfo.InvariantCulture) };
            foreach (var (s, when) in local.Where(x => x.Local.Year == start.Year && x.Local.Month == start.Month))
            {
                if (s.Status == SessionStatus.Completed)
                {
                    row.Held++;
                    if (s.Feedback is { } f)
                    {
                        row.FeedbackCount++;
                        if (f.Goal == GoalResult.Yes) row.GoalYes++;
                        else if (f.Goal == GoalResult.Partially) row.GoalPartly++;
                        else row.GoalNo++;
                        if (f.StudentOnTime) row.OnTimeCount++;
                        if (f.DailyTaskGiven)
                        {
                            row.TasksGiven++;
                            if (f.DailyTaskSubmitted) row.TasksSubmitted++;
                        }
                    }
                }
                else if (s.Status == SessionStatus.NotHeld)
                {
                    if (s.NotHeldReason == NotHeldReason.StudentAbsent) row.Absent++;
                    else if (s.NotHeldReason == NotHeldReason.StudentExcused) row.Excused++;
                    else row.OtherNotHeld++;
                }
            }
            model.Rows.Add(row);
        }

        model.Total = new ProgressMonth
        {
            Label = "Total",
            Held = model.Rows.Sum(r => r.Held), Absent = model.Rows.Sum(r => r.Absent),
            Excused = model.Rows.Sum(r => r.Excused), OtherNotHeld = model.Rows.Sum(r => r.OtherNotHeld),
            FeedbackCount = model.Rows.Sum(r => r.FeedbackCount), GoalYes = model.Rows.Sum(r => r.GoalYes),
            GoalPartly = model.Rows.Sum(r => r.GoalPartly), GoalNo = model.Rows.Sum(r => r.GoalNo),
            OnTimeCount = model.Rows.Sum(r => r.OnTimeCount),
            TasksGiven = model.Rows.Sum(r => r.TasksGiven), TasksSubmitted = model.Rows.Sum(r => r.TasksSubmitted)
        };

        string GoalText(GoalResult g) => g switch { GoalResult.Yes => "Goal achieved", GoalResult.Partially => "Partly achieved", _ => "Not achieved" };
        string TrackText(OnTrackStatus t) => t switch { OnTrackStatus.OnTrack => "On track", OnTrackStatus.NeedsSupport => "Needs support", _ => "Behind" };
        model.Recent = local.Where(x => x.Session.Status == SessionStatus.Completed && x.Session.Feedback is not null)
            .OrderByDescending(x => x.Session.StartUtc).Take(8)
            .Select(x => new RecentLesson
            {
                LocalDate = x.Local, Title = string.IsNullOrWhiteSpace(x.Session.Feedback!.LessonTitle) ? "Lesson" : x.Session.Feedback.LessonTitle,
                Goal = GoalText(x.Session.Feedback.Goal), OnTrack = TrackText(x.Session.Feedback.OnTrack)
            }).ToList();

        return View(model);
    }
}
