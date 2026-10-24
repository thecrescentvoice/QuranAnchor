using Microsoft.EntityFrameworkCore;
using QuranAnchor.Application.Abstractions;
using QuranAnchor.Application.Common;
using QuranAnchor.Application.Models;
using QuranAnchor.Domain.Entities;
using QuranAnchor.Domain.Enums;
using QuranAnchor.Infrastructure.Persistence;

namespace QuranAnchor.Infrastructure.Services;

/// <summary>Calculates the monthly report and the tutor monthly summary from class records.</summary>
public class ReportService(QuranAnchorDbContext db, ISettingsService settings) : IReportService
{
    private static double? Rate(int part, int whole) => whole == 0 ? null : Math.Round(part * 100.0 / whole, 1);
    private static int Words(string? text) =>
        string.IsNullOrWhiteSpace(text) ? 0 : text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;

    public async Task<ReportData> BuildAsync(Guid studentId, int year, int month, CancellationToken ct = default)
    {
        var student = await db.Students.AsNoTracking().Include(s => s.Tutor).FirstAsync(s => s.Id == studentId, ct);
        var rules = await settings.GetSystemAsync(ct);
        var package = await db.Packages.AsNoTracking().FirstOrDefaultAsync(p => p.StudentId == studentId && p.Year == year && p.Month == month, ct);

        var data = new ReportData
        {
            StudentId = student.Id, StudentName = student.FullName, StudentCode = student.StudentCode,
            Level = student.Level.ToString(), TutorName = student.Tutor?.FullName ?? "", Year = year, Month = month,
            PackageTotal = package?.Total ?? 0, CarriedIn = package?.CarriedInSessions ?? 0
        };
        if (package is null) return data;

        var sessions = await db.Sessions.AsNoTracking().Include(s => s.Feedback)
            .Where(s => s.PackageId == package.Id).OrderBy(s => s.StartUtc).ToListAsync(ct);

        var held = sessions.Where(s => s.Status == SessionStatus.Completed).ToList();
        var notHeld = sessions.Where(s => s.Status == SessionStatus.NotHeld).ToList();
        var academyMissed = notHeld.Count(s => s.NotHeldReason is NotHeldReason.TutorUnavailable or NotHeldReason.Technical or NotHeldReason.Other);
        var studentMissed = notHeld.Count(s => s.NotHeldReason is NotHeldReason.StudentAbsent or NotHeldReason.StudentExcused);

        data.SessionsHeld = held.Count;
        data.SessionsNotHeld = notHeld.Count;
        data.SessionsScheduled = sessions.Count(s => s.Status is SessionStatus.Scheduled or SessionStatus.Completed or SessionStatus.NotHeld);

        data.ClassDeliveryRate = Rate(held.Count, held.Count + academyMissed);
        data.AttendanceRate = Rate(held.Count, held.Count + studentMissed);

        var fbs = held.Where(s => s.Feedback is not null).Select(s => s.Feedback!).ToList();
        data.PunctualityRate = Rate(fbs.Count(f => f.StudentOnTime), fbs.Count);
        data.OnTrackRate = Rate(fbs.Count(f => f.OnTrack == OnTrackStatus.OnTrack), fbs.Count);

        var withTask = fbs.Where(f => !string.IsNullOrWhiteSpace(f.TaskText)).ToList();
        data.AssignmentRate = Rate(withTask.Count(f => f.TaskSubmitted), withTask.Count);
        var withDaily = fbs.Where(f => f.DailyTaskGiven).ToList();
        data.DailyTaskRate = Rate(withDaily.Count(f => f.DailyTaskSubmitted), withDaily.Count);

        data.GoalYes = fbs.Count(f => f.Goal == GoalResult.Yes);
        data.GoalPartial = fbs.Count(f => f.Goal == GoalResult.Partially);
        data.GoalNo = fbs.Count(f => f.Goal == GoalResult.No);
        data.GoalRate = fbs.Count == 0 ? null : Math.Round((data.GoalYes + 0.5 * data.GoalPartial) * 100.0 / fbs.Count, 1);

        data.LessonDeliveryRate = Rate(fbs.Count(f => !string.IsNullOrWhiteSpace(f.LessonTitle)), fbs.Count);
        data.ResourceUsageRate = Rate(fbs.Count(f => !string.IsNullOrWhiteSpace(f.SlidesUrl) || !string.IsNullOrWhiteSpace(f.Worksheet)), fbs.Count);
        data.EngagementRate = Rate(fbs.Count(f => f.TaskProgress == OnTrackStatus.OnTrack), fbs.Count);
        data.FeedbackQualityRate = Rate(fbs.Count(f => Words(f.TeacherNote) >= rules.MinFeedbackWords), fbs.Count);

        data.StudentCameraOff = fbs.Count(f => !f.StudentCameraOn);
        data.StudentNetworkIssues = fbs.Count(f => !f.StudentNetworkStable);
        data.TeacherNetworkIssues = fbs.Count(f => !f.TeacherNetworkStable);
        data.EnvironmentIssues = fbs.Count(f => !f.TeacherEnvironmentConducive || !f.StudentEnvironmentConducive);

        // The overall figure averages only the measures that exist; anything not measured is left out, not counted as zero.
        var core = new[] { data.PunctualityRate, data.OnTrackRate, data.AssignmentRate, data.DailyTaskRate, data.GoalRate }
            .Where(v => v.HasValue).Select(v => v!.Value).ToList();
        if (core.Count > 0)
        {
            data.Overall = Math.Round(core.Average(), 1);
            data.Classification = data.Overall >= rules.StrongThresholdPercent ? "Excellent progress"
                : data.Overall >= rules.GoodThresholdPercent ? "Good progress" : "Needs attention";
        }

        foreach (var s in sessions.Where(x => x.Status is SessionStatus.Completed or SessionStatus.NotHeld))
        {
            data.Lines.Add(new ReportSessionLine
            {
                LocalStart = TimeHelper.ToLocal(s.StartUtc, student.TimeZoneId),
                Status = s.Status == SessionStatus.Completed ? "Held" : $"Not held ({s.NotHeldReason})",
                Lesson = s.Feedback?.LessonTitle ?? "",
                Goal = s.Feedback is null ? "" : s.Feedback.Goal.ToString(),
                Note = Trim(s.Feedback?.TeacherNote, 160)
            });
        }
        data.Topics = fbs.Select(f => f.LessonTitle).Where(t => !string.IsNullOrWhiteSpace(t)).Distinct().ToList();
        return data;
    }

    public async Task<List<TutorSummary>> TutorSummariesAsync(int year, int month, Guid? onlyTutorId, CancellationToken ct = default)
    {
        var tutors = await db.Tutors.AsNoTracking().Where(t => onlyTutorId == null || t.Id == onlyTutorId).OrderBy(t => t.FullName).ToListAsync(ct);
        var result = new List<TutorSummary>();
        var first = new DateOnly(year, month, 1);

        foreach (var t in tutors)
        {
            var from = TimeHelper.ToUtc(first, TimeOnly.MinValue, t.TimeZoneId);
            var to = TimeHelper.ToUtc(first.AddMonths(1), TimeOnly.MinValue, t.TimeZoneId);
            var sessions = await db.Sessions.AsNoTracking().Where(s => s.TutorId == t.Id && s.StartUtc >= from && s.StartUtc < to).ToListAsync(ct);
            var held = sessions.Where(s => s.Status == SessionStatus.Completed).ToList();

            result.Add(new TutorSummary
            {
                TutorId = t.Id, TutorName = t.FullName,
                StudentsTaught = sessions.Where(s => s.Status is SessionStatus.Completed or SessionStatus.Scheduled).Select(s => s.StudentId).Distinct().Count(),
                SessionsHeld = held.Count,
                SessionsNotHeld = sessions.Count(s => s.Status == SessionStatus.NotHeld),
                Hours = Math.Round(held.Sum(s => s.DurationMinutes) / 60.0, 2),
                FeedbackSubmitted = held.Count,
                FeedbackMissing = sessions.Count(s => s.Status == SessionStatus.Scheduled && s.EndUtc < DateTime.UtcNow),
                Amount = held.Sum(s => s.AmountEarned)
            });
        }
        return result;
    }

    private static string Trim(string? text, int max) =>
        string.IsNullOrEmpty(text) ? "" : text.Length <= max ? text : text[..max] + "…";
}
