using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using QuranAnchor.Application.Abstractions;
using QuranAnchor.Domain.Entities;
using QuranAnchor.Infrastructure.Persistence;

namespace QuranAnchor.Infrastructure.Services;

public class AuditLogger(QuranAnchorDbContext db, IHttpContextAccessor accessor) : IAuditLogger
{
    public async Task LogAsync(string action, string? target = null, string? details = null, CancellationToken ct = default)
    {
        var http = accessor.HttpContext;
        var principal = http?.User;

        db.AuditLogs.Add(new AuditLog
        {
            OccurredAtUtc = DateTime.UtcNow,
            UserId = Trim(principal?.FindFirstValue(ClaimTypes.NameIdentifier), 64),
            UserName = Trim(principal?.Identity?.Name, 256),
            Action = Trim(action, 100) ?? "Unknown",
            Target = Trim(target, 200),
            Details = Trim(details, 2000),
            IpAddress = Trim(http?.Connection.RemoteIpAddress?.ToString(), 64)
        });

        await db.SaveChangesAsync(ct);
    }

    private static string? Trim(string? value, int max) =>
        value is null ? null : value.Length <= max ? value : value[..max];
}
