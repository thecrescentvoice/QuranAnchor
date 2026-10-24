using Microsoft.AspNetCore.Identity;

namespace QuranAnchor.Infrastructure.Identity;

/// <summary>
/// One class for every kind of person. Students sign in with their Student ID
/// (stored as UserName); their Email is the parent's address and may be shared by siblings.
/// Staff sign in with their email (stored as UserName).
/// </summary>
public class ApplicationUser : IdentityUser<Guid>
{
    public string FullName { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public bool MustChangePassword { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? DeactivatedAtUtc { get; set; }

    // Privacy notice consent (used from week 2).
    public int? ConsentVersion { get; set; }
    public DateTime? ConsentAtUtc { get; set; }
}
