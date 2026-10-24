using System.Net;
using System.Text;
using Microsoft.EntityFrameworkCore;
using QuranAnchor.Application.Abstractions;
using QuranAnchor.Application.Common;
using QuranAnchor.Domain.Entities;
using QuranAnchor.Domain.Enums;
using QuranAnchor.Infrastructure.Persistence;

namespace QuranAnchor.Infrastructure.Services;

/// <summary>Feedback after each class, "class not held" marks, tutor pay per class and the WhatsApp text.</summary>
public class FeedbackService(
    QuranAnchorDbContext db,
    ISettingsService settings,
    INotificationService notify) : IFeedbackService
{
    public async Task<decimal> RateForAsync(Guid tutorId, int durationMinutes, CancellationToken ct = default)
    {
        var rates = await db.SessionRates.AsNoTracking()
            .Where(r => r.DurationMinutes == durationMinutes && (r.TutorId == null || r.TutorId == tutorId))
            .ToListAsync(ct);
        return rates.OrderByDescending(r => r.TutorId.HasValue).Select(r => r.Amount).FirstOrDefault();
    }

    public async Task<(bool Ok, string? Error)> SubmitAsync(SessionFeedback fb, Guid userId, CancellationToken ct = default)
    {
        var session = await db.Sessions.Include(s => s.Feedback).FirstOrDefaultAsync(s => s.Id == fb.SessionId, ct);
        if (session is null) return (false, "Class not found.");
        if (session.Status is SessionStatus.Rescheduled or SessionStatus.Cancelled)
            return (false, "This class was moved or cancelled.");
        if (session.StartUtc > DateTime.UtcNow.AddMinutes(5))
            return (false, "You can write the feedback once the class has started.");

        var existing = session.Feedback;
        if (existing is null)
        {
            fb.SubmittedByUserId = userId;
            fb.SubmittedAtUtc = DateTime.UtcNow;
            db.Feedback.Add(fb);
        }
        else
        {
            Copy(fb, existing);
            existing.SubmittedAtUtc = DateTime.UtcNow;
        }

        if (session.Status != SessionStatus.Completed)
        {
            session.Status = SessionStatus.Completed;
            session.NotHeldReason = null;
            session.NotHeldNote = string.Empty;
            session.UsesPackageSession = true;
            // The amount is saved on the class, so a later rate change never alters past months.
            session.AmountEarned = await RateForAsync(session.TutorId, session.DurationMinutes, ct);
        }

        await db.SaveChangesAsync(ct);
        return (true, null);
    }

    public async Task<(bool Ok, string? Error)> MarkNotHeldAsync(Guid sessionId, NotHeldReason reason, string note, CancellationToken ct = default)
    {
        var session = await db.Sessions.Include(s => s.Student).Include(s => s.Tutor).FirstOrDefaultAsync(s => s.Id == sessionId, ct);
        if (session?.Student is null || session.Tutor is null) return (false, "Class not found.");
        if (session.Status is SessionStatus.Rescheduled or SessionStatus.Cancelled) return (false, "This class was moved or cancelled.");
        if (session.Status == SessionStatus.Completed) return (false, "Feedback was already submitted for this class.");

        var rules = await settings.GetSystemAsync(ct);
        if (session.StartUtc > DateTime.UtcNow) return (false, "The class has not started yet.");
        if (reason == NotHeldReason.StudentAbsent && DateTime.UtcNow < session.StartUtc.AddMinutes(rules.NoShowWaitMinutes))
            return (false, $"Wait {rules.NoShowWaitMinutes} minutes after the start time before marking the student absent.");

        session.Status = SessionStatus.NotHeld;
        session.NotHeldReason = reason;
        session.NotHeldNote = note.Trim();
        session.AmountEarned = 0;
        session.UsesPackageSession = reason == NotHeldReason.StudentAbsent && rules.UnexcusedAbsenceUsesSession;
        await db.SaveChangesAsync(ct);

        var when = TimeHelper.Format(session.StartUtc, session.Student.TimeZoneId);
        if (reason is NotHeldReason.StudentAbsent)
        {
            var body = await notify.WrapAsync("Class marked as missed",
                $"The class for {WebUtility.HtmlEncode(session.Student.FullName)} on <strong>{when}</strong> was marked <strong>student absent</strong>.",
                "If this is a mistake, please use the Report an issue button in the portal.");
            await notify.SendToParentAsync(session.Student, "Class marked as missed", body, ct);
        }
        else if (reason is NotHeldReason.TutorUnavailable or NotHeldReason.Technical or NotHeldReason.Other)
        {
            var body = await notify.WrapAsync("A class did not hold",
                $"The class of {WebUtility.HtmlEncode(session.Student.FullName)} with {WebUtility.HtmlEncode(session.Tutor.FullName)} on {when} did not hold.",
                $"Reason: {reason}. {WebUtility.HtmlEncode(note)}");
            await notify.SendToSupervisorsAsync("A class did not hold", body, ct);
        }
        return (true, null);
    }

    public async Task<string> BuildWhatsAppTextAsync(Guid sessionId, CancellationToken ct = default)
    {
        var s = await db.Sessions.AsNoTracking().Include(x => x.Student).Include(x => x.Feedback).FirstOrDefaultAsync(x => x.Id == sessionId, ct);
        var f = s?.Feedback;
        if (s?.Student is null || f is null) return string.Empty;

        var package = await db.Packages.AsNoTracking().FirstAsync(p => p.Id == s.PackageId, ct);
        var count = await db.Sessions.CountAsync(x => x.PackageId == s.PackageId && x.UsesPackageSession && x.StartUtc <= s.StartUtc, ct);
        var date = TimeHelper.ToLocal(s.StartUtc, s.Student.TimeZoneId).ToString("dd/MM/yyyy");

        static string YesNo(bool v) => v ? "Yes ✓" : "No ✗";
        static string Goal(GoalResult g) => g switch { GoalResult.Yes => "Yes ✓", GoalResult.Partially => "Partially ◐", _ => "No ✗" };
        static string Track(OnTrackStatus t, string comment)
        {
            var label = t switch { OnTrackStatus.OnTrack => "On Track", OnTrackStatus.NeedsSupport => "Needs Support", _ => "Behind" };
            return string.IsNullOrWhiteSpace(comment) ? label : $"{label} – {comment}";
        }

        var sb = new StringBuilder();
        sb.AppendLine("✅ QUR'AN CLASS FEEDBACK").AppendLine();
        sb.AppendLine($"👤 Student Name: {s.Student.FullName}").AppendLine();
        sb.AppendLine($"📅 Date: {date}").AppendLine();
        sb.AppendLine($"📖 Today's Lesson: {f.LessonTitle}").AppendLine();
        sb.AppendLine($"✅ Goal Achieved: {Goal(f.Goal)}{(string.IsNullOrWhiteSpace(f.GoalNote) ? "" : " – " + f.GoalNote)}").AppendLine();
        sb.AppendLine($"📊 Sessions Count: {count}/{package.Total}").AppendLine();
        sb.AppendLine($"⏱️ Time Used: {f.TimeUsedMinutes} minutes").AppendLine();
        sb.AppendLine($"📈 Student On Track with Lesson: {Track(f.OnTrack, f.OnTrackComment)}").AppendLine();
        sb.AppendLine($"🎯 Student Tasks Progress: {Track(f.TaskProgress, f.TaskComment)}").AppendLine();
        sb.AppendLine($"📹 Teacher Camera: {(f.TeacherCameraOn ? "On" : "Off")}");
        sb.AppendLine($"📹 Student Camera: {(f.StudentCameraOn ? "On" : "Off")}").AppendLine();
        sb.AppendLine($"🌿 Teacher Environment: {(f.TeacherEnvironmentConducive ? "Conducive" : "Not conducive")}");
        sb.AppendLine($"🌿 Student Environment: {(f.StudentEnvironmentConducive ? "Conducive" : "Not conducive")}").AppendLine();
        sb.AppendLine($"🛰️ Teacher Network: {(f.TeacherNetworkStable ? "Stable" : "Unstable")}");
        sb.AppendLine($"🛰️ Student Network: {(f.StudentNetworkStable ? "Stable" : "Unstable")}").AppendLine();
        sb.AppendLine($"🌱 Daily Task Submitted (Voice Note and Written): {YesNo(f.DailyTaskSubmitted)}");
        sb.AppendLine($"🌱 Daily Task Given (Voice Note and Written): {YesNo(f.DailyTaskGiven)}").AppendLine();
        if (!string.IsNullOrWhiteSpace(f.SlidesUrl)) sb.AppendLine($"📝 Lesson Slides: {f.SlidesUrl}").AppendLine();
        if (!string.IsNullOrWhiteSpace(f.Worksheet)) sb.AppendLine($"📄 Worksheet: {f.Worksheet}").AppendLine();
        if (!string.IsNullOrWhiteSpace(f.TaskText)) sb.AppendLine($"📝 Task: {f.TaskText}").AppendLine();
        sb.AppendLine($"🤲 Teacher's Note: {f.TeacherNote}");
        return sb.ToString().TrimEnd();
    }

    private static void Copy(SessionFeedback from, SessionFeedback to)
    {
        to.LessonTitle = from.LessonTitle; to.LearningObjective = from.LearningObjective;
        to.Goal = from.Goal; to.GoalNote = from.GoalNote; to.TimeUsedMinutes = from.TimeUsedMinutes;
        to.OnTrack = from.OnTrack; to.OnTrackComment = from.OnTrackComment;
        to.TaskProgress = from.TaskProgress; to.TaskComment = from.TaskComment;
        to.StudentOnTime = from.StudentOnTime;
        to.TeacherCameraOn = from.TeacherCameraOn; to.StudentCameraOn = from.StudentCameraOn;
        to.TeacherEnvironmentConducive = from.TeacherEnvironmentConducive; to.StudentEnvironmentConducive = from.StudentEnvironmentConducive;
        to.TeacherNetworkStable = from.TeacherNetworkStable; to.StudentNetworkStable = from.StudentNetworkStable;
        to.DailyTaskGiven = from.DailyTaskGiven; to.DailyTaskSubmitted = from.DailyTaskSubmitted;
        to.TaskText = from.TaskText; to.TaskSubmitted = from.TaskSubmitted;
        to.SlidesUrl = from.SlidesUrl; to.Worksheet = from.Worksheet; to.TeacherNote = from.TeacherNote;
    }
}
