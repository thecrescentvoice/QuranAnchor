namespace QuranAnchor.Infrastructure.Email;

public class EmailOptions
{
    /// <summary>"Logging" (prints emails to the console, for development) or "Resend".</summary>
    public string Provider { get; set; } = "Logging";
    public string ApiKey { get; set; } = string.Empty;
    /// <summary>An address on your verified domain, for example no-reply@yourdomain.com.</summary>
    public string FromAddress { get; set; } = string.Empty;
}
