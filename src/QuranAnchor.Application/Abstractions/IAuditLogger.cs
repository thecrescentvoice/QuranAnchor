namespace QuranAnchor.Application.Abstractions;

public interface IAuditLogger
{
    Task LogAsync(string action, string? target = null, string? details = null, CancellationToken ct = default);
}
