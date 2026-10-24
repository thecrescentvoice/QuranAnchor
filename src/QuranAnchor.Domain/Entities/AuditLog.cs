namespace QuranAnchor.Domain.Entities;

/// <summary>One row for every sensitive action (sign-ins, account changes, settings changes...).</summary>
public class AuditLog
{
    public long Id { get; set; }
    public DateTime OccurredAtUtc { get; set; } = DateTime.UtcNow;
    public string? UserId { get; set; }
    public string? UserName { get; set; }
    public string Action { get; set; } = string.Empty;
    public string? Target { get; set; }
    public string? Details { get; set; }
    public string? IpAddress { get; set; }
}
