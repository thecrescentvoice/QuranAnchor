using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using QuranAnchor.Application.Abstractions;

namespace QuranAnchor.Infrastructure.Email;

/// <summary>Sends email through Resend's HTTP API. The sender name always follows the academy name setting.</summary>
public class ResendEmailSender(
    HttpClient http,
    IOptions<EmailOptions> options,
    ISettingsService settings,
    ILogger<ResendEmailSender> logger) : IEmailSender
{
    public async Task SendAsync(string toEmail, string subject, string htmlBody, CancellationToken ct = default)
    {
        var o = options.Value;
        if (string.IsNullOrWhiteSpace(o.ApiKey) || string.IsNullOrWhiteSpace(o.FromAddress))
        {
            logger.LogWarning("Email to {To} was not sent: Email:ApiKey or Email:FromAddress is not set.", toEmail);
            return;
        }

        var branding = await settings.GetBrandingAsync(ct);

        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.resend.com/emails");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", o.ApiKey);
        request.Content = JsonContent.Create(new
        {
            from = $"{branding.AcademyName} <{o.FromAddress}>",
            to = new[] { toEmail },
            subject,
            html = htmlBody
        });

        try
        {
            using var response = await http.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(ct);
                logger.LogError("Email to {To} failed: {Status} {Body}", toEmail, (int)response.StatusCode, body);
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            logger.LogError(ex, "Email to {To} failed.", toEmail);
        }
    }
}
