using QuranAnchor.Application.Models;

namespace QuranAnchor.Application.Abstractions;

public interface ISettingsService
{
    Task<BrandingSettings> GetBrandingAsync(CancellationToken ct = default);
    Task SaveBrandingAsync(BrandingSettings branding, string? updatedBy, CancellationToken ct = default);

    Task<SystemSettings> GetSystemAsync(CancellationToken ct = default);
    Task SaveSystemAsync(SystemSettings settings, string? updatedBy, CancellationToken ct = default);
}
