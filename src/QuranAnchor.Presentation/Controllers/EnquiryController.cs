using System.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.RateLimiting;
using QuranAnchor.Application.Abstractions;
using QuranAnchor.Application.Common;
using QuranAnchor.Domain.Entities;
using QuranAnchor.Infrastructure.Persistence;
using QuranAnchor.Presentation.ViewModels;

namespace QuranAnchor.Presentation.Controllers;

/// <summary>The public page where a family asks about classes or a trial class. No sign-in needed.</summary>
[AllowAnonymous]
public class EnquiryController(
    QuranAnchorDbContext db,
    INotificationService notify,
    IEmailSender email,
    ISettingsService settings,
    IAuditLogger audit) : Controller
{
    private void FillZones() =>
        ViewBag.Zones = TimeHelper.CommonZones.Select(z => new SelectListItem(z.Replace('_', ' '), z)).ToList();

    [HttpGet]
    public IActionResult Index()
    {
        FillZones();
        return View(new EnquiryFormViewModel());
    }

    [HttpPost, ValidateAntiForgeryToken, EnableRateLimiting("auth")]
    public async Task<IActionResult> Index(EnquiryFormViewModel model)
    {
        // Robots fill in the hidden field. Pretend it worked, and save nothing.
        if (!string.IsNullOrWhiteSpace(model.Website)) return RedirectToAction(nameof(Thanks));

        if (!model.Agree) ModelState.AddModelError(nameof(model.Agree), "Please tick the box to continue.");
        if (!ModelState.IsValid)
        {
            FillZones();
            return View(model);
        }

        var enquiry = new Enquiry
        {
            ParentName = model.ParentName.Trim(), Email = model.Email.Trim(), Phone = model.Phone.Trim(),
            StudentName = model.StudentName.Trim(), StudentAge = model.StudentAge, Country = model.Country.Trim(),
            TimeZoneId = model.TimeZoneId, Level = model.Level,
            PreferredTimes = model.PreferredTimes?.Trim() ?? "", Message = model.Message?.Trim() ?? ""
        };
        db.Enquiries.Add(enquiry);
        await db.SaveChangesAsync();
        await audit.LogAsync("EnquiryReceived", enquiry.StudentName, enquiry.Country);

        var brand = await settings.GetBrandingAsync();
        var staffBody = await notify.WrapAsync("New enquiry",
            $"{WebUtility.HtmlEncode(enquiry.ParentName)} asked about classes for <strong>{WebUtility.HtmlEncode(enquiry.StudentName)}</strong> ({WebUtility.HtmlEncode(enquiry.Country)}).",
            "Open the portal, go to Enquiries and press “Take this enquiry”, so the other supervisors know you are looking after it.");
        await notify.SendToSupervisorsAsync("New class enquiry", staffBody);

        var parentBody = await notify.WrapAsync("We received your enquiry",
            $"Assalamu alaikum {WebUtility.HtmlEncode(enquiry.ParentName)}, thank you for contacting {WebUtility.HtmlEncode(brand.AcademyName)}.",
            $"We received your request for {WebUtility.HtmlEncode(enquiry.StudentName)} and will contact you soon about a trial class.");
        await email.SendAsync(enquiry.Email, "We received your enquiry", parentBody);

        return RedirectToAction(nameof(Thanks));
    }

    public IActionResult Thanks() => View();
}
