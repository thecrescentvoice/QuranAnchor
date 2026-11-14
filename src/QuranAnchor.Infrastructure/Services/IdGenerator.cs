using Microsoft.EntityFrameworkCore;
using QuranAnchor.Application.Abstractions;
using QuranAnchor.Domain.Entities;
using QuranAnchor.Infrastructure.Persistence;

namespace QuranAnchor.Infrastructure.Services;

/// <summary>Makes IDs like QA-STU-0007. The prefix comes from the branding settings, and existing IDs never change.</summary>
public class IdGenerator(QuranAnchorDbContext db, ISettingsService settings) : IIdGenerator
{
    public Task<string> NextStudentCodeAsync(CancellationToken ct = default) => NextAsync("STU", ct);
    public Task<string> NextTutorCodeAsync(CancellationToken ct = default) => NextAsync("TUT", ct);

    public Task<string> NextInvoiceNumberAsync(CancellationToken ct = default) => NextAsync("INV", ct, 5);

    private async Task<string> NextAsync(string kind, CancellationToken ct, int width = 4)
    {
        var branding = await settings.GetBrandingAsync(ct);

        var counter = await db.IdCounters.FirstOrDefaultAsync(c => c.Key == kind, ct);
        if (counter is null)
        {
            counter = new IdCounter { Key = kind, LastNumber = 0 };
            db.IdCounters.Add(counter);
        }

        counter.LastNumber++;
        await db.SaveChangesAsync(ct);
        return $"{branding.IdPrefix}-{kind}-{counter.LastNumber.ToString().PadLeft(width, '0')}";
    }
}
