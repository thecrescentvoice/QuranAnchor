using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace QuranAnchor.Infrastructure.Persistence;

/// <summary>
/// Runs the database setup when the web app starts (not when the EF tools build the host).
/// A failure stops the app from starting, so a broken database is noticed straight away.
/// </summary>
public sealed class DatabaseInitializerHostedService(IServiceProvider services, IConfiguration config) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken) =>
        DatabaseInitializer.InitializeAsync(services, config);

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
