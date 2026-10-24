using QuranAnchor.Domain.Enums;

namespace QuranAnchor.Domain.Entities;

/// <summary>A student. The login (Identity user) is linked by UserId; the parent uses that login.</summary>
public class StudentProfile
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public string StudentCode { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string ParentName { get; set; } = string.Empty;
    public string ParentEmail { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
    public string TimeZoneId { get; set; } = "Africa/Lagos";
    public StudentLevel Level { get; set; } = StudentLevel.Beginner;
    public int SessionsPerMonth { get; set; } = 8;
    public string MeetingLink { get; set; } = string.Empty;
    public DateTime? MeetingLinkChangedAtUtc { get; set; }
    public string PreferredCurrency { get; set; } = "NGN";
    public bool IsFeeExempt { get; set; }
    public DateTime? PaidUntilUtc { get; set; }
    public AccountStatus Status { get; set; } = AccountStatus.Active;
    public DateOnly? HoldUntil { get; set; }
    public Guid? TutorId { get; set; }
    public TutorProfile? Tutor { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

public class TutorProfile
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public string TutorCode { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string TimeZoneId { get; set; } = "Africa/Lagos";
    public AccountStatus Status { get; set; } = AccountStatus.Active;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

/// <summary>History of which tutor taught which student.</summary>
public class TutorAssignment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid StudentId { get; set; }
    public StudentProfile? Student { get; set; }
    public Guid TutorId { get; set; }
    public TutorProfile? Tutor { get; set; }
    public DateOnly StartedOn { get; set; }
    public DateOnly? EndedOn { get; set; }
}
