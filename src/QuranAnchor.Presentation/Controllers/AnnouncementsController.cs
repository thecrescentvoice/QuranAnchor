using System.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuranAnchor.Application.Abstractions;
using QuranAnchor.Domain.Constants;
using QuranAnchor.Domain.Entities;
using QuranAnchor.Domain.Enums;
using QuranAnchor.Infrastructure.Persistence;
using QuranAnchor.Presentation.Helpers;
using QuranAnchor.Presentation.Services;
using QuranAnchor.Presentation.ViewModels;

namespace QuranAnchor.Presentation.Controllers;

[Authorize]
public class AnnouncementsController(
    QuranAnchorDbContext db,
    INotificationService notify,
    IEmailSender email,
    ISettingsService settings,
    IWebHostEnvironment env,
    IAuditLogger audit,
    CurrentUser me) : Controller
{
    public async Task<IActionResult> Index(int page = 1)
    {
        var audience = me.IsStudent ? new[] { AnnouncementAudience.Everyone, AnnouncementAudience.Students }
            : me.IsTutor ? new[] { AnnouncementAudience.Everyone, AnnouncementAudience.Tutors }
            : new[] { AnnouncementAudience.Everyone, AnnouncementAudience.Students, AnnouncementAudience.Tutors };
        var (items, info) = await db.Announcements.AsNoTracking().Where(a => audience.Contains(a.Audience)).OrderByDescending(a => a.CreatedAtUtc).PageAsync(page, 10);
        ViewBag.PageInfo = info;
        return View(items);
    }

    // ---------- Create ----------

    [Authorize(Policy = Permissions.PostAnnouncements)]
    [HttpGet]
    public IActionResult Create() => View(new AnnouncementFormViewModel());

    [Authorize(Policy = Permissions.PostAnnouncements)]
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(AnnouncementFormViewModel model)
    {
        FlyerStorage.Saved? flyer = null;
        if (model.Flyer is { Length: > 0 })
        {
            var (file, error) = await FlyerStorage.SaveAsync(model.Flyer, env.WebRootPath);
            if (error is not null) ModelState.AddModelError(nameof(model.Flyer), error);
            flyer = file;
        }
        if (!ModelState.IsValid)
        {
            if (flyer is not null) FlyerStorage.Delete(flyer.Path, env.WebRootPath);
            return View(model);
        }

        var announcement = new Announcement
        {
            Title = model.Title.Trim(), Body = model.Body.Trim(), Audience = model.Audience,
            CreatedBy = User.FindFirst(AppClaimTypes.FullName)?.Value ?? User.Identity?.Name ?? "",
            FlyerPath = flyer?.Path ?? "", FlyerName = flyer?.Name ?? "", FlyerKind = flyer?.Kind ?? ""
        };
        db.Announcements.Add(announcement);
        await db.SaveChangesAsync();
        await audit.LogAsync("AnnouncementPosted", model.Title);

        if (model.SendEmail) await EmailAsync(announcement);

        TempData["Saved"] = "Announcement posted.";
        return RedirectToAction(nameof(Index));
    }

    // ---------- Edit ----------

    [Authorize(Policy = Permissions.PostAnnouncements)]
    [HttpGet]
    public async Task<IActionResult> Edit(Guid id)
    {
        var a = await db.Announcements.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (a is null) return NotFound();
        return View(new AnnouncementFormViewModel
        {
            Id = a.Id, Title = a.Title, Body = a.Body, Audience = a.Audience,
            ExistingFlyerPath = a.FlyerPath, ExistingFlyerName = a.FlyerName, ExistingFlyerKind = a.FlyerKind
        });
    }

    [Authorize(Policy = Permissions.PostAnnouncements)]
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Guid id, AnnouncementFormViewModel model)
    {
        var a = await db.Announcements.FirstOrDefaultAsync(x => x.Id == id);
        if (a is null) return NotFound();
        model.Id = id;
        model.ExistingFlyerPath = a.FlyerPath; model.ExistingFlyerName = a.FlyerName; model.ExistingFlyerKind = a.FlyerKind;

        FlyerStorage.Saved? flyer = null;
        if (model.Flyer is { Length: > 0 })
        {
            var (file, error) = await FlyerStorage.SaveAsync(model.Flyer, env.WebRootPath);
            if (error is not null) ModelState.AddModelError(nameof(model.Flyer), error);
            flyer = file;
        }
        if (!ModelState.IsValid)
        {
            if (flyer is not null) FlyerStorage.Delete(flyer.Path, env.WebRootPath);
            return View(model);
        }

        a.Title = model.Title.Trim(); a.Body = model.Body.Trim(); a.Audience = model.Audience;
        if (flyer is not null)
        {
            FlyerStorage.Delete(a.FlyerPath, env.WebRootPath);
            a.FlyerPath = flyer.Path; a.FlyerName = flyer.Name; a.FlyerKind = flyer.Kind;
        }
        else if (model.RemoveFlyer)
        {
            FlyerStorage.Delete(a.FlyerPath, env.WebRootPath);
            a.FlyerPath = ""; a.FlyerName = ""; a.FlyerKind = "";
        }
        a.UpdatedAtUtc = DateTime.UtcNow;
        a.UpdatedBy = User.FindFirst(AppClaimTypes.FullName)?.Value ?? User.Identity?.Name ?? "";
        await db.SaveChangesAsync();
        await audit.LogAsync("AnnouncementEdited", a.Title);

        TempData["Saved"] = "Announcement updated.";
        return RedirectToAction(nameof(Index));
    }

    // ---------- Delete ----------

    [Authorize(Policy = Permissions.PostAnnouncements)]
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid id)
    {
        var a = await db.Announcements.FirstOrDefaultAsync(x => x.Id == id);
        if (a is null) return NotFound();
        FlyerStorage.Delete(a.FlyerPath, env.WebRootPath);
        db.Announcements.Remove(a);
        await db.SaveChangesAsync();
        await audit.LogAsync("AnnouncementDeleted", a.Title);
        TempData["Saved"] = "Announcement deleted.";
        return RedirectToAction(nameof(Index));
    }

    // ---------- Email ----------

    private async Task EmailAsync(Announcement a)
    {
        var baseUrl = Request.Scheme + "://" + Request.Host;
        var paragraphs = new List<string> { Fmt.LinkifyHtml(a.Body, lineBreaks: true) };
        if (!string.IsNullOrEmpty(a.FlyerPath)) paragraphs.Add($"<a href=\"{baseUrl}{a.FlyerPath}\">View the flyer</a>");
        var body = await notify.WrapAsync(a.Title, paragraphs.ToArray());

        var addresses = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (a.Audience is AnnouncementAudience.Everyone or AnnouncementAudience.Students)
            foreach (var x in await db.Students.Where(s => s.Status == AccountStatus.Active).Select(s => s.ParentEmail).ToListAsync()) addresses.Add(x);
        if (a.Audience is AnnouncementAudience.Everyone or AnnouncementAudience.Tutors)
            foreach (var x in await db.Tutors.Where(t => t.Status == AccountStatus.Active).Select(t => t.Email).ToListAsync()) addresses.Add(x);
        foreach (var address in addresses.Where(x => !string.IsNullOrWhiteSpace(x))) await email.SendAsync(address, a.Title, body);
    }
}
