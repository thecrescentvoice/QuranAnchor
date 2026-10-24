using System.Net;
using Microsoft.AspNetCore.Identity;
using QuranAnchor.Application.Abstractions;
using QuranAnchor.Domain.Constants;
using QuranAnchor.Domain.Entities;
using QuranAnchor.Infrastructure.Identity;

namespace QuranAnchor.Infrastructure.Services;

/// <summary>Builds the academy-styled emails and sends them.</summary>
public class NotificationService(
    IEmailSender email,
    ISettingsService settings,
    UserManager<ApplicationUser> users) : INotificationService
{
    public Task SendToParentAsync(StudentProfile student, string subject, string bodyHtml, CancellationToken ct = default) =>
        string.IsNullOrWhiteSpace(student.ParentEmail)
            ? Task.CompletedTask
            : email.SendAsync(student.ParentEmail, subject, bodyHtml, ct);

    public Task SendToTutorAsync(TutorProfile tutor, string subject, string bodyHtml, CancellationToken ct = default) =>
        string.IsNullOrWhiteSpace(tutor.Email)
            ? Task.CompletedTask
            : email.SendAsync(tutor.Email, subject, bodyHtml, ct);

    public async Task SendToSupervisorsAsync(string subject, string bodyHtml, CancellationToken ct = default)
    {
        var supervisors = await users.GetUsersInRoleAsync(Roles.Supervisor);
        foreach (var s in supervisors.Where(u => u.IsActive && !string.IsNullOrWhiteSpace(u.Email)))
        {
            await email.SendAsync(s.Email!, subject, bodyHtml, ct);
        }
    }

    public async Task<string> WrapAsync(string heading, params string[] paragraphs)
    {
        var b = await settings.GetBrandingAsync();
        var body = string.Concat(paragraphs.Select(p => $"<p style=\"margin:0 0 12px\">{p}</p>"));
        return $"""
            <div style="font-family:Arial,Helvetica,sans-serif;font-size:16px;color:#2F3B3E;line-height:1.5;max-width:560px">
              <div style="height:4px;background:linear-gradient(90deg,#8B5CFF,#5CE1E6,#D61F9F);margin-bottom:16px"></div>
              <h2 style="font-size:20px;margin:0 0 12px">{WebUtility.HtmlEncode(heading)}</h2>
              {body}
              <p style="color:#5B6B6F;font-size:14px;margin-top:20px">{WebUtility.HtmlEncode(b.AcademyName)}</p>
            </div>
            """;
    }
}
