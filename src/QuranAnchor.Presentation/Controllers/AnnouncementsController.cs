using System.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuranAnchor.Application.Abstractions;
using QuranAnchor.Domain.Constants;
using QuranAnchor.Domain.Entities;
using QuranAnchor.Domain.Enums;
using QuranAnchor.Infrastructure.Persistence;
using QuranAnchor.Presentation.Services;
using QuranAnchor.Presentation.ViewModels;

namespace QuranAnchor.Presentation.Controllers;

[Authorize]
public class AnnouncementsController(
    QuranAnchorDbContext db,
    INotificationService notify,
    IEmailSender email,
    IAuditLogger audit,
    CurrentUser me) : Controller
{
    public async Task<IActionResult> Index()
    {
        var audience = me.IsStudent ? new[] { AnnouncementAudience.Everyone, AnnouncementAudience.Students }
            : me.IsTutor ? new[] { AnnouncementAudience.Everyone, AnnouncementAudience.Tutors }
            : new[] { AnnouncementAudience.Everyone, AnnouncementAudience.Students, AnnouncementAudience.Tutors };
        return View(await db.Announcements.AsNoTracking().Where(a => audience.Contains(a.Audience)).OrderByDescending(a => a.CreatedAtUtc).Take(50).ToListAsync());
    }

    [Authorize(Policy = Permissions.PostAnnouncements)]
    [HttpGet]
    public IActionResult Create() => View(new AnnouncementFormViewModel());

    [Authorize(Policy = Permissions.PostAnnouncements)]
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(AnnouncementFormViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        db.Announcements.Add(new Announcement
        {
            Title = model.Title.Trim(), Body = model.Body.Trim(), Audience = model.Audience,
            CreatedBy = User.FindFirst(AppClaimTypes.FullName)?.Value ?? User.Identity?.Name ?? ""
        });
        await db.SaveChangesAsync();
        await audit.LogAsync("AnnouncementPosted", model.Title);

        if (model.SendEmail)
        {
            var body = await notify.WrapAsync(model.Title.Trim(), WebUtility.HtmlEncode(model.Body.Trim()).Replace("\n", "<br />"));
            var addresses = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (model.Audience is AnnouncementAudience.Everyone or AnnouncementAudience.Students)
                foreach (var a in await db.Students.Where(s => s.Status == AccountStatus.Active).Select(s => s.ParentEmail).ToListAsync()) addresses.Add(a);
            if (model.Audience is AnnouncementAudience.Everyone or AnnouncementAudience.Tutors)
                foreach (var a in await db.Tutors.Where(t => t.Status == AccountStatus.Active).Select(t => t.Email).ToListAsync()) addresses.Add(a);
            foreach (var address in addresses.Where(a => !string.IsNullOrWhiteSpace(a))) await email.SendAsync(address, model.Title.Trim(), body);
        }

        TempData["Saved"] = "Announcement posted.";
        return RedirectToAction(nameof(Index));
    }
}
