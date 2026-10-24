namespace QuranAnchor.Application.Models;

/// <summary>
/// Everything the Super Admin can rename or restyle. The defaults below are only used
/// until the Super Admin saves something on the Branding page.
/// </summary>
public sealed record BrandingSettings
{
    public string AcademyName { get; init; } = "Quran Anchor";
    public string ShortName { get; init; } = "Quran Anchor";
    public string Tagline { get; init; } = "Steady in recitation, firm in progress.";
    public string ContactEmail { get; init; } = "";
    /// <summary>International format, digits only, no plus sign (for example 2348012345678).</summary>
    public string SupervisorWhatsApp { get; init; } = "";
    /// <summary>Used for new Student and Tutor IDs, for example QA-STU-0007.</summary>
    public string IdPrefix { get; init; } = "QA";
    public string PrimaryColor { get; init; } = "#5B3FD9";
    public string AccentColor { get; init; } = "#5CE1E6";
    public string LogoPath { get; init; } = "";
}
