using QuranAnchor.Domain.Enums;

namespace QuranAnchor.Domain.Entities;

/// <summary>What the monthly portal fee costs in each currency (NGN, USD, GBP).</summary>
public class PortalPrice
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Currency { get; set; } = "NGN";
    public decimal Amount { get; set; }
    public bool IsActive { get; set; } = true;
}

public class Payment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid StudentId { get; set; }
    public StudentProfile? Student { get; set; }
    public string Provider { get; set; } = "Paystack";
    public string Reference { get; set; } = string.Empty;
    public string Currency { get; set; } = "NGN";
    public decimal Amount { get; set; }
    public PaymentStatus Status { get; set; } = PaymentStatus.Pending;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? PaidAtUtc { get; set; }
    public DateTime? PeriodStartUtc { get; set; }
    public DateTime? PeriodEndUtc { get; set; }
    public bool IsManual { get; set; }
    public string Note { get; set; } = string.Empty;
}

/// <summary>What a tutor earns for one class of a given length. TutorId empty means every tutor.</summary>
public class SessionRate
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public int DurationMinutes { get; set; }
    public decimal Amount { get; set; }
    public Guid? TutorId { get; set; }
}

public class Announcement
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public AnnouncementAudience Audience { get; set; } = AnnouncementAudience.Everyone;
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
