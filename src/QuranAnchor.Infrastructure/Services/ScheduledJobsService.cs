using System.Net;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using QuranAnchor.Application.Abstractions;
using QuranAnchor.Application.Common;
using QuranAnchor.Application.Models;
using QuranAnchor.Domain.Entities;
using QuranAnchor.Domain.Enums;
using QuranAnchor.Infrastructure.Persistence;

namespace QuranAnchor.Infrastructure.Services;

/// <summary>
/// Runs once a minute in the background: creates upcoming classes, resumes paused students, and sends
/// class reminders, the tutors' morning list, missing-feedback alerts, fee reminders and carry-over notices.
/// Every email is recorded, so nothing is ever sent twice.
/// </summary>
public sealed class ScheduledJobsService(IServiceScopeFactory scopes, ILogger<ScheduledJobsService> logger) : BackgroundService
{
    private DateTime _lastGenerate = DateTime.MinValue;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try { await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken); }
        catch (OperationCanceledException) { return; }

        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
        do
        {
            try { await RunOnceAsync(stoppingToken); }
            catch (Exception ex) when (ex is not OperationCanceledException) { logger.LogError(ex, "Scheduled jobs failed."); }
        }
        while (await NextTickAsync(timer, stoppingToken));
    }

    private static async Task<bool> NextTickAsync(PeriodicTimer timer, CancellationToken ct)
    {
        try { return await timer.WaitForNextTickAsync(ct); }
        catch (OperationCanceledException) { return false; }
    }

    private async Task RunOnceAsync(CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<QuranAnchorDbContext>();
        var scheduling = sp.GetRequiredService<ISchedulingService>();
        var notify = sp.GetRequiredService<INotificationService>();
        var settings = sp.GetRequiredService<ISettingsService>();
        var baseUrl = (sp.GetRequiredService<IConfiguration>()["App:BaseUrl"] ?? string.Empty).TrimEnd('/');

        var rules = await settings.GetSystemAsync(ct);
        var now = DateTime.UtcNow;

        if (now - _lastGenerate > TimeSpan.FromMinutes(30))
        {
            _lastGenerate = now;
            await GenerateAsync(db, scheduling, ct);
        }

        await SendClassRemindersAsync(db, notify, rules, baseUrl, now, ct);
        await SendTutorDigestsAsync(db, notify, baseUrl, now, ct);
        await SendMissingFeedbackAsync(db, notify, rules, baseUrl, now, ct);
        await SendFeeRemindersAsync(db, notify, rules, baseUrl, now, ct);
        await SendCarryOverNoticesAsync(db, notify, now, ct);
        await SendInvoiceRemindersAsync(db, notify, settings, now, ct);
        await SendMeetingRemindersAsync(db, notify, now, ct);
    }

    private static async Task<bool> ClaimAsync(QuranAnchorDbContext db, string key, CancellationToken ct)
    {
        if (await db.SentNotifications.AnyAsync(n => n.Key == key, ct)) return false;
        db.SentNotifications.Add(new SentNotification { Key = key });
        try
        {
            await db.SaveChangesAsync(ct);
            return true;
        }
        catch (DbUpdateException)
        {
            foreach (var entry in db.ChangeTracker.Entries<SentNotification>().Where(e => e.State == EntityState.Added).ToList())
                entry.State = EntityState.Detached;
            return false;
        }
    }

    private static async Task GenerateAsync(QuranAnchorDbContext db, ISchedulingService scheduling, CancellationToken ct)
    {
        var holds = await db.Students.Where(s => s.Status == AccountStatus.OnHold && s.HoldUntil != null).ToListAsync(ct);
        foreach (var h in holds)
        {
            if (TimeHelper.LocalToday(h.TimeZoneId) > h.HoldUntil) await scheduling.ResumeAsync(h.Id, ct);
        }

        var ids = await db.Students.Where(s => s.Status == AccountStatus.Active).Select(s => s.Id).ToListAsync(ct);
        foreach (var id in ids) await scheduling.GenerateSessionsAsync(id, ct);
    }

    private static async Task SendClassRemindersAsync(QuranAnchorDbContext db, INotificationService notify, SystemSettings rules, string baseUrl, DateTime now, CancellationToken ct)
    {
        var due = await db.Sessions.Include(s => s.Student).Include(s => s.Tutor)
            .Where(s => s.Status == SessionStatus.Scheduled && s.StartUtc > now && s.StartUtc <= now.AddMinutes(rules.ParentReminderMinutes))
            .ToListAsync(ct);

        foreach (var s in due)
        {
            if (s.Student is null || s.Tutor is null) continue;
            if (!await ClaimAsync(db, $"reminder:{s.Id}", ct)) continue;

            var body = await notify.WrapAsync("Class reminder",
                $"{WebUtility.HtmlEncode(s.Student.FullName)} has a class with {WebUtility.HtmlEncode(s.Tutor.FullName)} at <strong>{TimeHelper.Format(s.StartUtc, s.Student.TimeZoneId)}</strong>.",
                $"<a href=\"{baseUrl}/Sessions/Join/{s.Id}\">Open the portal to join the class</a>");
            await notify.SendToParentAsync(s.Student, "Class reminder", body, ct);
        }
    }

    private static async Task SendTutorDigestsAsync(QuranAnchorDbContext db, INotificationService notify, string baseUrl, DateTime now, CancellationToken ct)
    {
        var tutors = await db.Tutors.Where(t => t.Status == AccountStatus.Active).ToListAsync(ct);
        foreach (var t in tutors)
        {
            var local = TimeHelper.ToLocal(now, t.TimeZoneId);
            if (local.Hour != 6) continue;   // the morning list goes out at 6 AM the tutor's time

            var day = DateOnly.FromDateTime(local);
            var from = TimeHelper.ToUtc(day, TimeOnly.MinValue, t.TimeZoneId);
            var to = from.AddDays(1);
            var today = await db.Sessions.Include(s => s.Student)
                .Where(s => s.TutorId == t.Id && s.Status == SessionStatus.Scheduled && s.StartUtc >= from && s.StartUtc < to)
                .OrderBy(s => s.StartUtc).ToListAsync(ct);
            if (today.Count == 0) continue;
            if (!await ClaimAsync(db, $"digest:{t.Id}:{day:yyyyMMdd}", ct)) continue;

            var lines = today.Select(s => $"{TimeHelper.ToLocal(s.StartUtc, t.TimeZoneId):h:mm tt}: {WebUtility.HtmlEncode(s.Student?.FullName)} ({s.DurationMinutes} min)").ToArray();
            var body = await notify.WrapAsync("Today's classes", string.Join("<br />", lines), $"<a href=\"{baseUrl}\">Open the portal</a>");
            await notify.SendToTutorAsync(t, "Your classes today", body, ct);
        }
    }

    private static async Task SendMissingFeedbackAsync(QuranAnchorDbContext db, INotificationService notify, SystemSettings rules, string baseUrl, DateTime now, CancellationToken ct)
    {
        var pending = await db.Sessions.Include(s => s.Student).Include(s => s.Tutor)
            .Where(s => s.Status == SessionStatus.Scheduled && s.StartUtc < now && s.StartUtc > now.AddDays(-14))
            .ToListAsync(ct);

        var alerts = new List<(Guid? Supervisor, string Line)>();
        foreach (var s in pending.Where(p => p.EndUtc <= now.AddHours(-rules.TutorFeedbackReminderHours)))
        {
            if (s.Student is null || s.Tutor is null) continue;
            if (!await ClaimAsync(db, $"fbtutor:{s.Id}", ct)) continue;

            var body = await notify.WrapAsync("Feedback needed",
                $"Please write the feedback for {WebUtility.HtmlEncode(s.Student.FullName)} (class of {TimeHelper.Format(s.StartUtc, s.Tutor.TimeZoneId)}), or mark the class as not held.",
                $"<a href=\"{baseUrl}/Sessions/Details/{s.Id}\">Open the class</a>");
            await notify.SendToTutorAsync(s.Tutor, "Feedback needed", body, ct);
        }

        foreach (var s in pending.Where(p => p.EndUtc <= now.AddHours(-rules.SupervisorAlertHours)))
        {
            if (s.Student is null || s.Tutor is null) continue;
            if (!await ClaimAsync(db, $"fbsup:{s.Id}", ct)) continue;
            alerts.Add((s.Tutor.SupervisorId, $"{WebUtility.HtmlEncode(s.Tutor.FullName)}: {WebUtility.HtmlEncode(s.Student.FullName)}, {TimeHelper.Format(s.StartUtc, s.Tutor.TimeZoneId)}"));
        }

        // Each supervisor is told only about their own tutors.
        foreach (var group in alerts.GroupBy(a => a.Supervisor))
        {
            var body = await notify.WrapAsync("Classes with no feedback", "These classes ended with no feedback and were not marked as not held:", string.Join("<br />", group.Select(a => a.Line)));
            await notify.SendToSupervisorAsync(group.Key, "Classes with no feedback", body, ct);
        }
    }

    private static async Task SendFeeRemindersAsync(QuranAnchorDbContext db, INotificationService notify, SystemSettings rules, string baseUrl, DateTime now, CancellationToken ct)
    {
        var due = await db.Students
            .Where(s => s.Status == AccountStatus.Active && !s.IsFeeExempt && s.PaidUntilUtc != null
                        && s.PaidUntilUtc <= now.AddDays(rules.FeeReminderDaysBefore) && s.PaidUntilUtc > now.AddDays(-30))
            .ToListAsync(ct);

        foreach (var s in due)
        {
            if (!await ClaimAsync(db, $"fee:{s.Id}:{s.PaidUntilUtc:yyyyMMdd}", ct)) continue;
            var body = await notify.WrapAsync("Portal fee due",
                $"The monthly portal fee for {WebUtility.HtmlEncode(s.FullName)} is due on <strong>{s.PaidUntilUtc:d MMM yyyy}</strong>.",
                $"<a href=\"{baseUrl}/Payments\">Pay now</a>");
            await notify.SendToParentAsync(s, "Portal fee due", body, ct);
        }
    }

    private static async Task SendMeetingRemindersAsync(QuranAnchorDbContext db, INotificationService notify, DateTime now, CancellationToken ct)
    {
        var meetings = await db.Meetings.Include(m => m.Invitees)
            .Where(m => !m.IsCancelled && m.StartUtc > now && m.StartUtc <= now.AddHours(25)).ToListAsync(ct);
        if (meetings.Count == 0) return;
        var tutors = await db.Tutors.Where(t => t.Status == AccountStatus.Active).ToListAsync(ct);

        foreach (var meeting in meetings)
        {
            var hoursLeft = (meeting.StartUtc - now).TotalHours;
            var kind = hoursLeft <= 1.25 ? "1h" : hoursLeft >= 23 ? "24h" : null;
            if (kind is null) continue;

            var invited = meeting.AllTutors
                ? tutors.Where(t => meeting.SupervisorId == null || t.SupervisorId == meeting.SupervisorId).ToList()
                : tutors.Where(t => meeting.Invitees.Any(i => i.TutorId == t.Id)).ToList();
            foreach (var tutor in invited)
            {
                if (!await ClaimAsync(db, $"meet{kind}:{meeting.Id}:{tutor.Id}", ct)) continue;
                var link = string.IsNullOrWhiteSpace(meeting.MeetingLink) ? "The meeting link will be shared before the meeting." : $"Join: <a href=\"{WebUtility.HtmlEncode(meeting.MeetingLink)}\">{WebUtility.HtmlEncode(meeting.MeetingLink)}</a>";
                var body = await notify.WrapAsync("Meeting reminder",
                    $"<strong>{WebUtility.HtmlEncode(meeting.Title)}</strong> starts {(kind == "1h" ? "in about an hour" : "tomorrow")}: {TimeHelper.Format(meeting.StartUtc, tutor.TimeZoneId)} ({TimeHelper.City(tutor.TimeZoneId)} time).", link);
                await notify.SendToTutorAsync(tutor, $"Reminder: {meeting.Title}", body, ct);
            }
        }
    }

    private static async Task SendInvoiceRemindersAsync(QuranAnchorDbContext db, INotificationService notify, ISettingsService settings, DateTime now, CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(now);
        var overdue = (await db.Invoices.Include(i => i.Student).Include(i => i.Payments)
            .Where(i => !i.IsCancelled && i.DueDate < today).OrderBy(i => i.DueDate).Take(200).ToListAsync(ct))
            .Where(i => i.Balance > 0).ToList();
        if (overdue.Count == 0) return;

        var brand = await settings.GetBrandingAsync(ct);
        foreach (var invoice in overdue)
        {
            if (invoice.Student is null) continue;
            if (!await ClaimAsync(db, $"invover:{invoice.Id}", ct)) continue;

            var how = string.IsNullOrWhiteSpace(brand.FeeInstructions)
                ? "Please follow the payment instructions you were given in the class group."
                : "How to pay: " + WebUtility.HtmlEncode(brand.FeeInstructions).Replace("\n", "<br />");
            var body = await notify.WrapAsync("Class fee reminder",
                $"Invoice {invoice.Number} for {WebUtility.HtmlEncode(invoice.Student.FullName)} was due on {invoice.DueDate:d MMM yyyy}.",
                $"The balance is <strong>{invoice.Currency} {invoice.Balance:N2}</strong>.", how,
                "If you have already paid, please send the proof of payment to the supervisor.");
            await notify.SendToParentAsync(invoice.Student, "Class fee reminder", body, ct);
        }
    }

    private static async Task SendCarryOverNoticesAsync(QuranAnchorDbContext db, INotificationService notify, DateTime now, CancellationToken ct)
    {
        var packages = await db.Packages.Include(p => p.Student).ThenInclude(s => s!.Tutor)
            .Where(p => p.CarriedInSessions > 0 && p.Year == now.Year && p.Month == now.Month).ToListAsync(ct);

        foreach (var p in packages)
        {
            if (p.Student is null) continue;
            if (!await ClaimAsync(db, $"carry:{p.Id}", ct)) continue;

            var body = await notify.WrapAsync("Sessions carried over",
                $"{WebUtility.HtmlEncode(p.Student.FullName)} has <strong>{p.CarriedInSessions}</strong> session(s) left from last month.",
                "Please agree with the parent how these classes will be held, then use Add a session if you need extra dates.");
            if (p.Student.Tutor is not null) await notify.SendToTutorAsync(p.Student.Tutor, "Sessions carried over", body, ct);
            await notify.SendToSupervisorAsync(p.Student.SupervisorId, "Sessions carried over", body, ct);
        }
    }
}
