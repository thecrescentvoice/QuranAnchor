using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QuranAnchor.Application.Abstractions;
using QuranAnchor.Application.Models;
using QuranAnchor.Domain.Constants;
using QuranAnchor.Presentation.ViewModels;

namespace QuranAnchor.Presentation.Controllers;

[Authorize(Policy = Permissions.EditBranding)]
public class SettingsController(
    ISettingsService settings,
    IAuditLogger audit,
    IWebHostEnvironment env) : Controller
{
    private static readonly string[] AllowedLogoTypes = { ".png", ".jpg", ".jpeg", ".webp" };
    private const long MaxLogoBytes = 1_000_000;

    [HttpGet]
    public async Task<IActionResult> Branding()
    {
        var current = await settings.GetBrandingAsync();
        return View(BrandingViewModel.From(current));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Branding(BrandingViewModel model)
    {
        var current = await settings.GetBrandingAsync();
        model.LogoPath = current.LogoPath;

        if (model.LogoFile is { Length: > 0 })
        {
            var extension = Path.GetExtension(model.LogoFile.FileName).ToLowerInvariant();
            if (!AllowedLogoTypes.Contains(extension))
            {
                ModelState.AddModelError(nameof(model.LogoFile), "Use a PNG, JPG or WebP image.");
            }
            else if (model.LogoFile.Length > MaxLogoBytes)
            {
                ModelState.AddModelError(nameof(model.LogoFile), "The logo must be smaller than 1 MB.");
            }
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var logoPath = current.LogoPath;
        var folder = Path.Combine(env.WebRootPath, "branding");

        if (model.LogoFile is { Length: > 0 })
        {
            Directory.CreateDirectory(folder);
            DeleteExistingLogos(folder);

            var fileName = "logo" + Path.GetExtension(model.LogoFile.FileName).ToLowerInvariant();
            await using (var stream = System.IO.File.Create(Path.Combine(folder, fileName)))
            {
                await model.LogoFile.CopyToAsync(stream);
            }

            // The version number makes browsers load the new logo straight away.
            logoPath = $"/branding/{fileName}?v={DateTime.UtcNow.Ticks}";
        }
        else if (model.RemoveLogo)
        {
            DeleteExistingLogos(folder);
            logoPath = string.Empty;
        }

        var updated = new BrandingSettings
        {
            AcademyName = model.AcademyName.Trim(),
            ShortName = model.ShortName.Trim(),
            Tagline = model.Tagline?.Trim() ?? string.Empty,
            ContactEmail = model.ContactEmail?.Trim() ?? string.Empty,
            SupervisorWhatsApp = model.SupervisorWhatsApp?.Trim() ?? string.Empty,
            IdPrefix = model.IdPrefix.Trim(),
            PrimaryColor = model.PrimaryColor,
            AccentColor = model.AccentColor,
            LogoPath = logoPath
        };

        await settings.SaveBrandingAsync(updated, User.Identity?.Name);
        await audit.LogAsync("BrandingUpdated", "Branding",
            $"Name: {current.AcademyName} -> {updated.AcademyName}; Prefix: {current.IdPrefix} -> {updated.IdPrefix}");

        TempData["Saved"] = "Branding saved. It now shows on every page, email and PDF.";
        return RedirectToAction(nameof(Branding));
    }

    private static void DeleteExistingLogos(string folder)
    {
        if (!Directory.Exists(folder))
        {
            return;
        }

        foreach (var file in Directory.GetFiles(folder, "logo.*"))
        {
            System.IO.File.Delete(file);
        }
    }
}
