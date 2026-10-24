using System.ComponentModel.DataAnnotations;

namespace QuranAnchor.Presentation.ViewModels;

public class LoginViewModel
{
    [Required(ErrorMessage = "Enter your Student ID or email.")]
    [Display(Name = "Student ID or email")]
    public string Identifier { get; set; } = string.Empty;

    [Required(ErrorMessage = "Enter your password.")]
    [DataType(DataType.Password)]
    [Display(Name = "Password")]
    public string Password { get; set; } = string.Empty;

    public string? ReturnUrl { get; set; }
}

public class ForgotPasswordViewModel
{
    [Required(ErrorMessage = "Enter your Student ID or email.")]
    [Display(Name = "Student ID or email")]
    public string Identifier { get; set; } = string.Empty;
}

public class ResetPasswordViewModel
{
    public Guid UserId { get; set; }
    public string Token { get; set; } = string.Empty;

    [Required(ErrorMessage = "Choose a new password.")]
    [StringLength(100, MinimumLength = 8, ErrorMessage = "Use at least 8 characters.")]
    [DataType(DataType.Password)]
    [Display(Name = "New password")]
    public string NewPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "Type the new password again.")]
    [DataType(DataType.Password)]
    [Compare(nameof(NewPassword), ErrorMessage = "The two passwords do not match.")]
    [Display(Name = "Confirm new password")]
    public string ConfirmPassword { get; set; } = string.Empty;
}

public class ChangePasswordViewModel
{
    [Required(ErrorMessage = "Enter your current password.")]
    [DataType(DataType.Password)]
    [Display(Name = "Current password")]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "Choose a new password.")]
    [StringLength(100, MinimumLength = 8, ErrorMessage = "Use at least 8 characters.")]
    [DataType(DataType.Password)]
    [Display(Name = "New password")]
    public string NewPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "Type the new password again.")]
    [DataType(DataType.Password)]
    [Compare(nameof(NewPassword), ErrorMessage = "The two passwords do not match.")]
    [Display(Name = "Confirm new password")]
    public string ConfirmPassword { get; set; } = string.Empty;

    /// <summary>True when the person has to do this before anything else (temporary password).</summary>
    public bool IsForced { get; set; }
}

public class TwoFactorCodeViewModel
{
    [Required(ErrorMessage = "Enter the 6-digit code from your authenticator app.")]
    [Display(Name = "Code from your authenticator app")]
    public string Code { get; set; } = string.Empty;
    public string? ReturnUrl { get; set; }
}

public class TwoFactorSetupViewModel
{
    public bool Enabled { get; set; }
    public string Key { get; set; } = string.Empty;
    public string Uri { get; set; } = string.Empty;

    [Display(Name = "Code from your authenticator app")]
    public string Code { get; set; } = string.Empty;
}

public class ConsentViewModel
{
    [Range(typeof(bool), "true", "true", ErrorMessage = "Please tick the box to continue.")]
    public bool Accepted { get; set; }
}
