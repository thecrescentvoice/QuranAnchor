using QuranAnchor.Domain.Enums;

namespace QuranAnchor.Domain.Entities;

/// <summary>One weekly slot, in the local time of TimeZoneId (so daylight saving never shifts a class).</summary>
public class ClassSchedule
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid StudentId { get; set; }
    public StudentProfile? Student { get; set; }
    public Guid TutorId { get; set; }
    public TutorProfile? Tutor { get; set; }
    public DayOfWeek DayOfWeek { get; set; }
    public TimeOnly LocalStartTime { get; set; }
    public string TimeZoneId { get; set; } = "Africa/Lagos";
    public int DurationMinutes { get; set; } = 30;
    public DateOnly EffectiveFrom { get; set; }
    public DateOnly? EffectiveTo { get; set; }
}

/// <summary>One calendar month of sessions for one student.</summary>
public class SessionPackage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid StudentId { get; set; }
    public StudentProfile? Student { get; set; }
    public int Year { get; set; }
    public int Month { get; set; }
    public int PlannedSessions { get; set; }
    public int CarriedInSessions { get; set; }
    public int Total => PlannedSessions + CarriedInSessions;
}

public class ClassSession
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid StudentId { get; set; }
    public StudentProfile? Student { get; set; }
    public Guid TutorId { get; set; }
    public TutorProfile? Tutor { get; set; }
    public Guid PackageId { get; set; }
    public SessionPackage? Package { get; set; }
    public Guid? ScheduleId { get; set; }
    public Guid? RescheduledFromSessionId { get; set; }
    public DateTime StartUtc { get; set; }
    public int DurationMinutes { get; set; } = 30;
    public SessionStatus Status { get; set; } = SessionStatus.Scheduled;
    public NotHeldReason? NotHeldReason { get; set; }
    public string NotHeldNote { get; set; } = string.Empty;
    /// <summary>True once the class is finished and it uses up one of the student's sessions.</summary>
    public bool UsesPackageSession { get; set; }
    public bool IsExtra { get; set; }
    public decimal AmountEarned { get; set; }
    public SessionFeedback? Feedback { get; set; }

    public DateTime EndUtc => StartUtc.AddMinutes(DurationMinutes);
}

public class RescheduleRequest
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SessionId { get; set; }
    public ClassSession? Session { get; set; }
    public Guid RequestedByUserId { get; set; }
    public bool RequestedByTutor { get; set; }
    public DateTime ProposedStartUtc { get; set; }
    public string Reason { get; set; } = string.Empty;
    public RequestStatus Status { get; set; } = RequestStatus.Pending;
    public Guid? RespondedByUserId { get; set; }
    public DateTime? RespondedAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

public class BreakDay
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public string Reason { get; set; } = string.Empty;
}

/// <summary>Remembers which emails were already sent so reminders never repeat.</summary>
public class SentNotification
{
    public long Id { get; set; }
    public string Key { get; set; } = string.Empty;
    public DateTime SentAtUtc { get; set; } = DateTime.UtcNow;
}

/// <summary>Counter for Student and Tutor IDs (STU, TUT).</summary>
public class IdCounter
{
    public string Key { get; set; } = string.Empty;
    public int LastNumber { get; set; }
}
