using System.ComponentModel.DataAnnotations;
using QuranAnchor.Application.Models;

namespace QuranAnchor.Presentation.ViewModels;

public class BrandingViewModel
{
    [Required(ErrorMessage = "Enter the academy name.")]
    [StringLength(80)]
    [Display(Name = "Academy name")]
    public string AcademyName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Enter a short name.")]
    [StringLength(40)]
    [Display(Name = "Short name")]
    public string ShortName { get; set; } = string.Empty;

    [StringLength(120)]
    [Display(Name = "Tagline")]
    public string Tagline { get; set; } = string.Empty;

    [EmailAddress(ErrorMessage = "Enter a valid email address.")]
    [StringLength(200)]
    [Display(Name = "Contact email")]
    public string ContactEmail { get; set; } = string.Empty;

    [RegularExpression(@"^(\d{8,15})?$", ErrorMessage = "Use digits only, in international format, with no plus sign (for example 2348012345678).")]
    [Display(Name = "Supervisor WhatsApp number")]
    public string SupervisorWhatsApp { get; set; } = string.Empty;

    [Required(ErrorMessage = "Enter an ID prefix.")]
    [RegularExpression(@"^[A-Z0-9]{2,6}$", ErrorMessage = "Use 2 to 6 capital letters or digits.")]
    [Display(Name = "ID prefix")]
    public string IdPrefix { get; set; } = string.Empty;

    [Required]
    [RegularExpression(@"^#[0-9a-fA-F]{6}$", ErrorMessage = "Use a colour like #5B3FD9.")]
    [Display(Name = "Main colour")]
    public string PrimaryColor { get; set; } = string.Empty;

    [Required]
    [RegularExpression(@"^#[0-9a-fA-F]{6}$", ErrorMessage = "Use a colour like #5CE1E6.")]
    [Display(Name = "Accent colour")]
    public string AccentColor { get; set; } = string.Empty;

    [Display(Name = "Logo")]
    public IFormFile? LogoFile { get; set; }

    [Display(Name = "Remove the current logo")]
    public bool RemoveLogo { get; set; }

    public string LogoPath { get; set; } = string.Empty;

    public static BrandingViewModel From(BrandingSettings b) => new()
    {
        AcademyName = b.AcademyName,
        ShortName = b.ShortName,
        Tagline = b.Tagline,
        ContactEmail = b.ContactEmail,
        SupervisorWhatsApp = b.SupervisorWhatsApp,
        IdPrefix = b.IdPrefix,
        PrimaryColor = b.PrimaryColor,
        AccentColor = b.AccentColor,
        LogoPath = b.LogoPath
    };
}
