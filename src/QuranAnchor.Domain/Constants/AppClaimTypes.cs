namespace QuranAnchor.Domain.Constants;

public static class AppClaimTypes
{
    public const string FullName = "qa:full_name";
    public const string MustChangePassword = "qa:must_change_password";
    public const string ConsentRequired = "qa:consent_required";
    public const string TwoFactorRequired = "qa:two_factor_required";
}

public static class PrivacyNotice
{
    /// <summary>Raise this number when the notice text changes, so everyone is asked to agree again.</summary>
    public const int CurrentVersion = 1;
}
