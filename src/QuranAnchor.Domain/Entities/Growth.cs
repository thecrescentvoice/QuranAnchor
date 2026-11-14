using QuranAnchor.Domain.Enums;

namespace QuranAnchor.Domain.Entities;

/// <summary>A family asking about classes, from the public sign-up form. It can become a student.</summary>
public class Enquiry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string ParentName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string StudentName { get; set; } = string.Empty;
    public int? StudentAge { get; set; }
    public string Country { get; set; } = string.Empty;
    public string TimeZoneId { get; set; } = "Africa/Lagos";
    public StudentLevel Level { get; set; } = StudentLevel.Beginner;
    public string PreferredTimes { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public EnquiryStatus Status { get; set; } = EnquiryStatus.New;
    public string Notes { get; set; } = string.Empty;
    public DateTime? TrialStartUtc { get; set; }
    public Guid? TrialTutorId { get; set; }
    public string TrialMeetingLink { get; set; } = string.Empty;
    /// <summary>The supervisor who took this enquiry. Empty until someone takes it; then only that supervisor (and admins) see it.</summary>
    public Guid? SupervisorId { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

/// <summary>A weekly window when a tutor can teach, in the tutor's own time zone.</summary>
public class TutorAvailability
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TutorId { get; set; }
    public TutorProfile? Tutor { get; set; }
    public DayOfWeek DayOfWeek { get; set; }
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public string Note { get; set; } = string.Empty;
}

/// <summary>A parent's rating of the tutor for one calendar month.</summary>
public class TutorRating
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid StudentId { get; set; }
    public StudentProfile? Student { get; set; }
    public Guid TutorId { get; set; }
    public TutorProfile? Tutor { get; set; }
    public int Year { get; set; }
    public int Month { get; set; }
    public int Stars { get; set; }
    public string Comment { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

/// <summary>A class fee invoice. Class fees are paid outside the portal (bank transfer); staff record what was received.</summary>
public class Invoice
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid StudentId { get; set; }
    public StudentProfile? Student { get; set; }
    public string Number { get; set; } = string.Empty;
    public int Year { get; set; }
    public int Month { get; set; }
    public string Description { get; set; } = string.Empty;
    public string Currency { get; set; } = "NGN";
    public decimal Amount { get; set; }
    public DateOnly DueDate { get; set; }
    public bool IsCancelled { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public List<InvoicePayment> Payments { get; set; } = new();

    public decimal PaidAmount => Payments.Sum(p => p.Amount);
    public decimal Balance => Amount - PaidAmount;

    public string StatusText(DateOnly today) =>
        IsCancelled ? "Cancelled"
        : Balance <= 0 ? "Paid"
        : DueDate < today ? "Overdue"
        : PaidAmount > 0 ? "Part paid" : "Unpaid";
}

public class InvoicePayment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid InvoiceId { get; set; }
    public Invoice? Invoice { get; set; }
    public decimal Amount { get; set; }
    public DateOnly PaidOn { get; set; }
    public string Method { get; set; } = "Bank transfer";
    public string Reference { get; set; } = string.Empty;
    public string RecordedBy { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

/// <summary>A meeting for tutors (staff meetings, training, briefings).</summary>
public class Meeting
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = string.Empty;
    public string Agenda { get; set; } = string.Empty;
    public DateTime StartUtc { get; set; }
    public int DurationMinutes { get; set; } = 60;
    /// <summary>The time zone the organiser used when entering the time. Each tutor sees it in their own zone.</summary>
    public string TimeZoneId { get; set; } = "Africa/Lagos";
    public string MeetingLink { get; set; } = string.Empty;
    /// <summary>True when every active tutor is invited. Otherwise only the tutors in Invitees.</summary>
    public bool AllTutors { get; set; } = true;
    /// <summary>Minutes or notes written after the meeting.</summary>
    public string Notes { get; set; } = string.Empty;
    public bool IsCancelled { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    /// <summary>Set when a supervisor created it: "every tutor" then means that supervisor's tutors. Empty when an admin created it.</summary>
    public Guid? SupervisorId { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public List<MeetingInvitee> Invitees { get; set; } = new();
}

public class MeetingInvitee
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid MeetingId { get; set; }
    public Meeting? Meeting { get; set; }
    public Guid TutorId { get; set; }
    public TutorProfile? Tutor { get; set; }
}
