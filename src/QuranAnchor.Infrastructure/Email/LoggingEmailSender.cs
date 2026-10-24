using Microsoft.Extensions.Logging;
using QuranAnchor.Application.Abstractions;

namespace QuranAnchor.Infrastructure.Email;

/// <summary>Development only: writes the email to the console so reset links can be copied from there.</summary>
public class LoggingEmailSender(ILogger<LoggingEmailSender> logger) : IEmailSender
{
    public Task SendAsync(string toEmail, string subject, string htmlBody, CancellationToken ct = default)
    {
        logger.LogInformation(
            "EMAIL (not sent, Logging provider) To: {To} | Subject: {Subject}\n{Body}",
            toEmail, subject, htmlBody);
        return Task.CompletedTask;
    }
}
