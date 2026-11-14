namespace QuranAnchor.Domain.Entities;

/// <summary>A certificate awarded to a student. Anyone can check one is genuine with its code.</summary>
public class Certificate
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid StudentId { get; set; }
    public StudentProfile? Student { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateOnly IssuedOn { get; set; }
    public string IssuedByName { get; set; } = string.Empty;
    public Guid? IssuedByUserId { get; set; }
    public bool IsRevoked { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
