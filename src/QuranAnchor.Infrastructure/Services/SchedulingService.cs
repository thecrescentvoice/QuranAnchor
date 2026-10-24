using System.Net;
using Microsoft.EntityFrameworkCore;
using QuranAnchor.Application.Abstractions;
using QuranAnchor.Application.Common;
using QuranAnchor.Application.Models;
using QuranAnchor.Domain.Entities;
using QuranAnchor.Domain.Enums;
using QuranAnchor.Infrastructure.Persistence;

namespace QuranAnchor.Infrastructure.Services;

/// <summary>Weekly schedules, monthly packages, session generation, break days, pauses and rescheduling.</summary>
public class SchedulingService(
    QuranAnchorDbContext db,
    INotificationService notify) : ISchedulingService
{
    private static bool CountsAsActive(ClassSession s) =>
        s.Status is SessionStatus.Scheduled or SessionStatus.Completed
        || (s.Status == SessionStatus.NotHeld && s.UsesPackageSession);

    // ---------- Packages ----------

    public async Task<SessionPackage> EnsurePackageAsync(StudentProfile student, int year, int month, int? plannedOverride = null, CancellationToken ct = default)
    {
        var existing = await db.Packages.FirstOrDefaultAsync(p => p.StudentId == student.Id && p.Year == year && p.Month == month, ct);
        if (existing is not null) return existing;

        var package = new SessionPackage
        {
            StudentId = student.Id,
            Year = year,
            Month = month,
            PlannedSessions = plannedOverride ?? student.SessionsPerMonth,
            CarriedInSessions = await LeftoverFromPreviousAsync(student.Id, year, month, ct)
        };
        db.Packages.Add(package);
        await db.SaveChangesAsync(ct);
        return package;
    }

    private async Task<int> LeftoverFromPreviousAsync(Guid studentId, int year, int month, CancellationToken ct)
    {
        var prev = new DateOnly(year, month, 1).AddMonths(-1);
        var package = await db.Packages.AsNoTracking()
            .FirstOrDefaultAsync(p => p.StudentId == studentId && p.Year == prev.Year && p.Month == prev.Month, ct);
        if (package is null) return 0;

        var now = DateTime.UtcNow;
        // Classes that already happened but still wait for feedback count as used, so they are never carried over by mistake.
        var used = await db.Sessions.CountAsync(s => s.PackageId == package.Id &&
            (s.UsesPackageSession || (s.Status == SessionStatus.Scheduled && s.StartUtc < now)), ct);
        return Math.Max(0, package.Total - used);
    }

    public async Task<PackageUsage> UsageAsync(Guid packageId, CancellationToken ct = default)
    {
        var package = await db.Packages.AsNoTracking().FirstAsync(p => p.Id == packageId, ct);
        var sessions = await db.Sessions.AsNoTracking().Where(s => s.PackageId == packageId).ToListAsync(ct);
        var used = sessions.Count(s => s.UsesPackageSession);
        var ahead = sessions.Count(s => s.Status == SessionStatus.Scheduled);
        return new PackageUsage(package.Total, used, ahead, Math.Max(0, package.Total - used - ahead));
    }

    // ---------- Session generation ----------

    public async Task<int> GenerateSessionsAsync(Guid studentId, CancellationToken ct = default)
    {
        var student = await db.Students.FirstOrDefaultAsync(s => s.Id == studentId, ct);
        if (student is null || student.Status != AccountStatus.Active || student.TutorId is null) return 0;

        var schedules = await db.Schedules.Where(s => s.StudentId == studentId).ToListAsync(ct);
        if (schedules.Count == 0) return 0;

        var breaks = await db.BreakDays.AsNoTracking().ToListAsync(ct);
        var today = TimeHelper.LocalToday(student.TimeZoneId);
        var monthEnd = new DateOnly(today.Year, today.Month, DateTime.DaysInMonth(today.Year, today.Month));
        var horizon = today.AddDays(28) < monthEnd ? today.AddDays(28) : monthEnd;

        // Sessions are only made for the current month, so leftover sessions can be carried over exactly at month end.
        var package = await EnsurePackageAsync(student, today.Year, today.Month, null, ct);
        var inPackage = await db.Sessions.Where(s => s.PackageId == package.Id).ToListAsync(ct);

        var created = 0;
        for (var date = today; date <= horizon; date = date.AddDays(1))
        {
            if (breaks.Any(b => date >= b.StartDate && date <= b.EndDate)) continue;

            foreach (var slot in schedules
                .Where(s => s.DayOfWeek == date.DayOfWeek && s.EffectiveFrom <= date && (s.EffectiveTo == null || s.EffectiveTo >= date))
                .OrderBy(s => s.LocalStartTime))
            {
                if (inPackage.Count(CountsAsActive) >= package.Total) goto done;

                var startUtc = TimeHelper.ToUtc(date, slot.LocalStartTime, slot.TimeZoneId);
                if (startUtc <= DateTime.UtcNow) continue;
                if (inPackage.Any(x => x.ScheduleId == slot.Id && x.StartUtc == startUtc)) continue;

                var session = new ClassSession
                {
                    StudentId = student.Id,
                    TutorId = slot.TutorId,
                    PackageId = package.Id,
                    ScheduleId = slot.Id,
                    StartUtc = startUtc,
                    DurationMinutes = slot.DurationMinutes
                };
                db.Sessions.Add(session);
                inPackage.Add(session);
                created++;
            }
        }

    done:
        if (created > 0) await db.SaveChangesAsync(ct);
        return created;
    }

    // ---------- Double-booking ----------

    public async Task<ClassSession?> FindConflictAsync(Guid tutorId, DateTime startUtc, int durationMinutes, Guid? excludeSessionId = null, CancellationToken ct = default)
    {
        var endUtc = startUtc.AddMinutes(durationMinutes);
        var nearby = await db.Sessions.AsNoTracking().Include(s => s.Student)
            .Where(s => s.TutorId == tutorId && s.Status == SessionStatus.Scheduled
                        && s.StartUtc < endUtc && s.StartUtc > startUtc.AddHours(-12)
                        && (excludeSessionId == null || s.Id != excludeSessionId))
            .ToListAsync(ct);
        return nearby.FirstOrDefault(s => s.EndUtc > startUtc);
    }

    private async Task<string?> SlotConflictAsync(Guid tutorId, Guid? excludeScheduleId, DayOfWeek day, TimeOnly time, string tz, int duration, DateOnly from, CancellationToken ct)
    {
        var date = from;
        while (date.DayOfWeek != day) date = date.AddDays(1);

        var start = TimeHelper.ToUtc(date, time, tz);
        var end = start.AddMinutes(duration);

        var others = await db.Schedules.AsNoTracking().Include(s => s.Student)
            .Where(s => s.TutorId == tutorId && (excludeScheduleId == null || s.Id != excludeScheduleId)
                        && s.DayOfWeek == day && (s.EffectiveTo == null || s.EffectiveTo >= from))
            .ToListAsync(ct);

        foreach (var o in others)
        {
            var oStart = TimeHelper.ToUtc(date, o.LocalStartTime, o.TimeZoneId);
            if (oStart < end && oStart.AddMinutes(o.DurationMinutes) > start)
            {
                return $"This tutor already teaches {o.Student?.FullName ?? "another student"} at that time.";
            }
        }

        var clash = await FindConflictAsync(tutorId, start, duration, null, ct);
        return clash is null ? null : $"This tutor already has a class with {clash.Student?.FullName ?? "another student"} at that time.";
    }

    // ---------- Weekly schedule ----------

    public async Task<(bool Ok, string? Error)> AddScheduleAsync(Guid studentId, DayOfWeek day, TimeOnly time, int duration, DateOnly from, CancellationToken ct = default)
    {
        var student = await db.Students.FirstOrDefaultAsync(s => s.Id == studentId, ct);
        if (student is null) return (false, "Student not found.");
        if (student.TutorId is null) return (false, "Assign a tutor first.");

        var clash = await SlotConflictAsync(student.TutorId.Value, null, day, time, student.TimeZoneId, duration, from, ct);
        if (clash is not null) return (false, clash);

        db.Schedules.Add(new ClassSchedule
        {
            StudentId = student.Id,
            TutorId = student.TutorId.Value,
            DayOfWeek = day,
            LocalStartTime = time,
            TimeZoneId = student.TimeZoneId,
            DurationMinutes = duration,
            EffectiveFrom = from
        });
        await db.SaveChangesAsync(ct);
        await GenerateSessionsAsync(student.Id, ct);
        return (true, null);
    }

    public async Task<(bool Ok, string? Error)> ChangeScheduleAsync(Guid scheduleId, DayOfWeek day, TimeOnly time, int duration, DateOnly from, CancellationToken ct = default)
    {
        var old = await db.Schedules.Include(s => s.Student).FirstOrDefaultAsync(s => s.Id == scheduleId, ct);
        if (old?.Student is null) return (false, "Schedule not found.");

        var clash = await SlotConflictAsync(old.TutorId, old.Id, day, time, old.TimeZoneId, duration, from, ct);
        if (clash is not null) return (false, clash);

        var fromUtc = TimeHelper.ToUtc(from, TimeOnly.MinValue, old.TimeZoneId);
        var future = await db.Sessions.Where(s => s.ScheduleId == old.Id && s.Status == SessionStatus.Scheduled && s.StartUtc >= fromUtc).ToListAsync(ct);
        db.Sessions.RemoveRange(future);

        if (from <= old.EffectiveFrom)
        {
            old.DayOfWeek = day; old.LocalStartTime = time; old.DurationMinutes = duration;
        }
        else
        {
            old.EffectiveTo = from.AddDays(-1);
            db.Schedules.Add(new ClassSchedule
            {
                StudentId = old.StudentId, TutorId = old.TutorId, DayOfWeek = day, LocalStartTime = time,
                TimeZoneId = old.TimeZoneId, DurationMinutes = duration, EffectiveFrom = from
            });
        }
        await db.SaveChangesAsync(ct);
        await GenerateSessionsAsync(old.StudentId, ct);

        var body = await notify.WrapAsync("Class day or time changed",
            $"The weekly class for {WebUtility.HtmlEncode(old.Student.FullName)} now takes place on <strong>{day}</strong> at <strong>{time:h:mm tt}</strong> ({TimeHelper.City(old.TimeZoneId)} time), starting from {from:d MMM yyyy}.",
            "If this is not what you agreed with the tutor, please tell the supervisor.");
        await notify.SendToParentAsync(old.Student, "Your weekly class time has changed", body, ct);
        return (true, null);
    }

    public async Task RemoveScheduleAsync(Guid scheduleId, DateOnly from, CancellationToken ct = default)
    {
        var slot = await db.Schedules.FirstOrDefaultAsync(s => s.Id == scheduleId, ct);
        if (slot is null) return;

        var fromUtc = TimeHelper.ToUtc(from, TimeOnly.MinValue, slot.TimeZoneId);
        var future = await db.Sessions.Where(s => s.ScheduleId == slot.Id && s.Status == SessionStatus.Scheduled && s.StartUtc >= fromUtc).ToListAsync(ct);
        db.Sessions.RemoveRange(future);
        slot.EffectiveTo = from.AddDays(-1);
        await db.SaveChangesAsync(ct);
    }

    public async Task<(bool Ok, string? Error)> AddExtraSessionAsync(Guid studentId, DateTime startUtc, int duration, CancellationToken ct = default)
    {
        var student = await db.Students.FirstOrDefaultAsync(s => s.Id == studentId, ct);
        if (student is null) return (false, "Student not found.");
        if (student.TutorId is null) return (false, "Assign a tutor first.");
        if (startUtc <= DateTime.UtcNow) return (false, "Choose a time in the future.");

        var clash = await FindConflictAsync(student.TutorId.Value, startUtc, duration, null, ct);
        if (clash is not null) return (false, $"The tutor already has a class with {clash.Student?.FullName} at that time.");

        var local = TimeHelper.ToLocal(startUtc, student.TimeZoneId);
        var package = await EnsurePackageAsync(student, local.Year, local.Month, null, ct);
        db.Sessions.Add(new ClassSession
        {
            StudentId = student.Id, TutorId = student.TutorId.Value, PackageId = package.Id,
            StartUtc = startUtc, DurationMinutes = duration, IsExtra = true
        });
        await db.SaveChangesAsync(ct);

        var body = await notify.WrapAsync("An extra class was scheduled",
            $"An extra class for {WebUtility.HtmlEncode(student.FullName)} is set for <strong>{TimeHelper.Format(startUtc, student.TimeZoneId)}</strong>.");
        await notify.SendToParentAsync(student, "An extra class was scheduled", body, ct);
        return (true, null);
    }

    // ---------- Break days and pauses ----------

    public async Task AddBreakDayAsync(BreakDay day, CancellationToken ct = default)
    {
        db.BreakDays.Add(day);

        var from = day.StartDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc).AddHours(-14);
        var to = day.EndDate.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc).AddHours(14);
        var affected = await db.Sessions.Include(s => s.Student)
            .Where(s => s.Status == SessionStatus.Scheduled && s.StartUtc >= from && s.StartUtc <= to).ToListAsync(ct);

        foreach (var s in affected)
        {
            var local = DateOnly.FromDateTime(TimeHelper.ToLocal(s.StartUtc, s.Student?.TimeZoneId ?? "Africa/Lagos"));
            if (local >= day.StartDate && local <= day.EndDate)
            {
                s.Status = SessionStatus.Cancelled;
                s.NotHeldReason = NotHeldReason.BreakDay;
                s.NotHeldNote = day.Reason;
            }
        }
        await db.SaveChangesAsync(ct);
    }

    public async Task RemoveBreakDayAsync(Guid id, CancellationToken ct = default)
    {
        var day = await db.BreakDays.FirstOrDefaultAsync(b => b.Id == id, ct);
        if (day is null) return;
        db.BreakDays.Remove(day);
        await db.SaveChangesAsync(ct);

        var ids = await db.Students.Where(s => s.Status == AccountStatus.Active).Select(s => s.Id).ToListAsync(ct);
        foreach (var studentId in ids) await GenerateSessionsAsync(studentId, ct);
    }

    public async Task SetOnHoldAsync(Guid studentId, DateOnly? until, CancellationToken ct = default)
    {
        var student = await db.Students.FirstOrDefaultAsync(s => s.Id == studentId, ct);
        if (student is null) return;

        student.Status = AccountStatus.OnHold;
        student.HoldUntil = until;

        var future = await db.Sessions.Where(s => s.StudentId == studentId && s.Status == SessionStatus.Scheduled && s.StartUtc > DateTime.UtcNow).ToListAsync(ct);
        foreach (var s in future)
        {
            s.Status = SessionStatus.Cancelled;
            s.NotHeldReason = NotHeldReason.Other;
            s.NotHeldNote = "Student on hold";
        }
        await db.SaveChangesAsync(ct);
    }

    public async Task ResumeAsync(Guid studentId, CancellationToken ct = default)
    {
        var student = await db.Students.FirstOrDefaultAsync(s => s.Id == studentId, ct);
        if (student is null) return;
        student.Status = AccountStatus.Active;
        student.HoldUntil = null;
        await db.SaveChangesAsync(ct);
        await GenerateSessionsAsync(studentId, ct);
    }

    // ---------- Rescheduling one class (both sides must agree) ----------

    public async Task<(bool Ok, string? Error)> RequestRescheduleAsync(Guid sessionId, Guid userId, bool byTutor, DateTime proposedStartUtc, string reason, CancellationToken ct = default)
    {
        var session = await db.Sessions.Include(s => s.Student).Include(s => s.Tutor).FirstOrDefaultAsync(s => s.Id == sessionId, ct);
        if (session?.Student is null || session.Tutor is null) return (false, "Class not found.");
        if (session.Status != SessionStatus.Scheduled || session.StartUtc <= DateTime.UtcNow) return (false, "Only upcoming classes can be moved.");
        if (proposedStartUtc <= DateTime.UtcNow) return (false, "Choose a time in the future.");
        if (await db.RescheduleRequests.AnyAsync(r => r.SessionId == sessionId && r.Status == RequestStatus.Pending, ct))
            return (false, "There is already a pending request for this class.");

        var clash = await FindConflictAsync(session.TutorId, proposedStartUtc, session.DurationMinutes, session.Id, ct);
        if (clash is not null) return (false, "The tutor is not free at that time.");

        db.RescheduleRequests.Add(new RescheduleRequest
        {
            SessionId = sessionId, RequestedByUserId = userId, RequestedByTutor = byTutor,
            ProposedStartUtc = proposedStartUtc, Reason = reason.Trim()
        });
        await db.SaveChangesAsync(ct);

        var who = byTutor ? "The tutor" : "The student or parent";
        if (byTutor)
        {
            var body = await notify.WrapAsync("A class change was requested",
                $"{who} asked to move the class of <strong>{TimeHelper.Format(session.StartUtc, session.Student.TimeZoneId)}</strong> to <strong>{TimeHelper.Format(proposedStartUtc, session.Student.TimeZoneId)}</strong>.",
                $"Reason: {WebUtility.HtmlEncode(reason)}", "Please sign in to accept or decline.");
            await notify.SendToParentAsync(session.Student, "A class change was requested", body, ct);
        }
        else
        {
            var body = await notify.WrapAsync("A class change was requested",
                $"{who} for {WebUtility.HtmlEncode(session.Student.FullName)} asked to move the class of <strong>{TimeHelper.Format(session.StartUtc, session.Tutor.TimeZoneId)}</strong> to <strong>{TimeHelper.Format(proposedStartUtc, session.Tutor.TimeZoneId)}</strong>.",
                $"Reason: {WebUtility.HtmlEncode(reason)}", "Please sign in to accept or decline.");
            await notify.SendToTutorAsync(session.Tutor, "A class change was requested", body, ct);
        }
        return (true, null);
    }

    public async Task<(bool Ok, string? Error)> RespondRescheduleAsync(Guid requestId, Guid userId, bool accept, bool isOverride, CancellationToken ct = default)
    {
        var request = await db.RescheduleRequests.Include(r => r.Session).ThenInclude(s => s!.Student).Include(r => r.Session).ThenInclude(s => s!.Tutor)
            .FirstOrDefaultAsync(r => r.Id == requestId, ct);
        var session = request?.Session;
        if (request is null || session?.Student is null || session.Tutor is null) return (false, "Request not found.");
        if (request.Status != RequestStatus.Pending) return (false, "This request was already answered.");

        request.RespondedByUserId = userId;
        request.RespondedAtUtc = DateTime.UtcNow;

        if (!accept)
        {
            request.Status = RequestStatus.Declined;
            await db.SaveChangesAsync(ct);
            await NotifyOutcomeAsync(request, session, "declined", ct);
            return (true, null);
        }

        if (session.Status != SessionStatus.Scheduled || request.ProposedStartUtc <= DateTime.UtcNow)
            return (false, "The class or the proposed time has already passed.");

        var clash = await FindConflictAsync(session.TutorId, request.ProposedStartUtc, session.DurationMinutes, session.Id, ct);
        if (clash is not null) return (false, "The tutor is no longer free at that time.");

        var local = TimeHelper.ToLocal(request.ProposedStartUtc, session.Student.TimeZoneId);
        var package = await EnsurePackageAsync(session.Student, local.Year, local.Month, null, ct);

        session.Status = SessionStatus.Rescheduled;   // the old slot does not count, so the class counts only once
        db.Sessions.Add(new ClassSession
        {
            StudentId = session.StudentId, TutorId = session.TutorId, PackageId = package.Id,
            RescheduledFromSessionId = session.Id, StartUtc = request.ProposedStartUtc,
            DurationMinutes = session.DurationMinutes, IsExtra = session.IsExtra
        });
        request.Status = RequestStatus.Accepted;
        await db.SaveChangesAsync(ct);
        await NotifyOutcomeAsync(request, session, "accepted", ct);
        return (true, null);
    }

    private async Task NotifyOutcomeAsync(RescheduleRequest request, ClassSession session, string outcome, CancellationToken ct)
    {
        var student = session.Student!;
        var tutor = session.Tutor!;
        var subject = $"Class change {outcome}";
        var detail = outcome == "accepted"
            ? $"The class is now on <strong>{TimeHelper.Format(request.ProposedStartUtc, student.TimeZoneId)}</strong> ({TimeHelper.Both(request.ProposedStartUtc, tutor.TimeZoneId, student.TimeZoneId)})."
            : "The class stays at its original time.";
        var body = await notify.WrapAsync(subject, $"The request to move the class of {TimeHelper.Format(session.StartUtc, student.TimeZoneId)} was {outcome}.", detail);
        await notify.SendToParentAsync(student, subject, body, ct);
        await notify.SendToTutorAsync(tutor, subject, body, ct);
    }
}
