using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using QuranAnchor.Application.Abstractions;
using QuranAnchor.Application.Models;
using QuranAnchor.Domain.Constants;
using QuranAnchor.Domain.Entities;
using QuranAnchor.Infrastructure.Persistence;

namespace QuranAnchor.Infrastructure.Services;

/// <summary>Reads and saves the Super Admin settings. Branding is cached so pages stay fast.</summary>
public class SettingsService(QuranAnchorDbContext db, IMemoryCache cache) : ISettingsService
{
    private const string BrandingCacheKey = "settings.branding";
    private const string SystemCacheKey = "settings.system";
    private static readonly Regex HexColor = new("^#[0-9a-fA-F]{6}$", RegexOptions.Compiled);

    public async Task<BrandingSettings> GetBrandingAsync(CancellationToken ct = default)
    {
        if (cache.TryGetValue(BrandingCacheKey, out BrandingSettings? cached) && cached is not null)
        {
            return cached;
        }

        var values = await db.AppSettings.AsNoTracking()
            .ToDictionaryAsync(s => s.Key, s => s.Value, ct);

        var defaults = new BrandingSettings();

        // Text that must not be empty falls back to the default.
        string Required(string key, string fallback) =>
            values.TryGetValue(key, out var v) && !string.IsNullOrWhiteSpace(v) ? v : fallback;

        // Text that may be empty (for example no logo, no contact email).
        string Optional(string key, string fallback) =>
            values.TryGetValue(key, out var v) ? v : fallback;

        // Colours are checked so a bad value can never end up inside the page's CSS.
        string Color(string key, string fallback)
        {
            var v = Required(key, fallback);
            return HexColor.IsMatch(v) ? v : fallback;
        }

        var branding = new BrandingSettings
        {
            AcademyName = Required(SettingKeys.AcademyName, defaults.AcademyName),
            ShortName = Required(SettingKeys.ShortName, defaults.ShortName),
            Tagline = Optional(SettingKeys.Tagline, defaults.Tagline),
            ContactEmail = Optional(SettingKeys.ContactEmail, defaults.ContactEmail),
            SupervisorWhatsApp = Optional(SettingKeys.SupervisorWhatsApp, defaults.SupervisorWhatsApp),
            IdPrefix = Required(SettingKeys.IdPrefix, defaults.IdPrefix),
            PrimaryColor = Color(SettingKeys.PrimaryColor, defaults.PrimaryColor),
            AccentColor = Color(SettingKeys.AccentColor, defaults.AccentColor),
            LogoPath = Optional(SettingKeys.LogoPath, defaults.LogoPath)
        };

        cache.Set(BrandingCacheKey, branding, TimeSpan.FromMinutes(30));
        return branding;
    }

    public async Task SaveBrandingAsync(BrandingSettings branding, string? updatedBy, CancellationToken ct = default)
    {
        await Upsert(SettingKeys.AcademyName, branding.AcademyName, updatedBy, ct);
        await Upsert(SettingKeys.ShortName, branding.ShortName, updatedBy, ct);
        await Upsert(SettingKeys.Tagline, branding.Tagline, updatedBy, ct);
        await Upsert(SettingKeys.ContactEmail, branding.ContactEmail, updatedBy, ct);
        await Upsert(SettingKeys.SupervisorWhatsApp, branding.SupervisorWhatsApp, updatedBy, ct);
        await Upsert(SettingKeys.IdPrefix, branding.IdPrefix, updatedBy, ct);
        await Upsert(SettingKeys.PrimaryColor, branding.PrimaryColor, updatedBy, ct);
        await Upsert(SettingKeys.AccentColor, branding.AccentColor, updatedBy, ct);
        await Upsert(SettingKeys.LogoPath, branding.LogoPath, updatedBy, ct);

        await db.SaveChangesAsync(ct);
        cache.Remove(BrandingCacheKey);
    }

    public async Task<SystemSettings> GetSystemAsync(CancellationToken ct = default)
    {
        if (cache.TryGetValue(SystemCacheKey, out SystemSettings? cached) && cached is not null)
        {
            return cached;
        }

        var row = await db.AppSettings.AsNoTracking().FirstOrDefaultAsync(s => s.Key == SettingKeys.SystemJson, ct);
        var settings = new SystemSettings();
        if (row is not null && !string.IsNullOrWhiteSpace(row.Value))
        {
            try { settings = JsonSerializer.Deserialize<SystemSettings>(row.Value) ?? settings; }
            catch (JsonException) { /* a damaged value falls back to the defaults */ }
        }

        cache.Set(SystemCacheKey, settings, TimeSpan.FromMinutes(30));
        return settings;
    }

    public async Task SaveSystemAsync(SystemSettings settings, string? updatedBy, CancellationToken ct = default)
    {
        await Upsert(SettingKeys.SystemJson, JsonSerializer.Serialize(settings), updatedBy, ct);
        await db.SaveChangesAsync(ct);
        cache.Remove(SystemCacheKey);
    }

    private async Task Upsert(string key, string value, string? updatedBy, CancellationToken ct)
    {
        var row = await db.AppSettings.FindAsync(new object[] { key }, ct);
        if (row is null)
        {
            db.AppSettings.Add(new AppSetting
            {
                Key = key,
                Value = value ?? string.Empty,
                UpdatedAtUtc = DateTime.UtcNow,
                UpdatedBy = updatedBy
            });
        }
        else
        {
            row.Value = value ?? string.Empty;
            row.UpdatedAtUtc = DateTime.UtcNow;
            row.UpdatedBy = updatedBy;
        }
    }
}
