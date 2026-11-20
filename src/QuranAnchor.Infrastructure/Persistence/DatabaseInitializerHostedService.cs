using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace QuranAnchor.Infrastructure.Persistence;

/// <summary>Runs database setup before the application starts serving requests.</summary>
public sealed class DatabaseInitializerHostedService(
    IServiceProvider services,
    IConfiguration config,
    ILogger<DatabaseInitializerHostedService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            await DatabaseInitializer.InitializeAsync(services, config, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Keep the original exception (including its provider error) in the structured log;
            // never include connection strings or credential values in the diagnostic message.
            logger.LogCritical(ex,
                "Database initialization failed. Verify Railway database networking, ConnectionStrings__Default, " +
                "Database__ServerVersion, schema/migration state, and database user privileges.");
            throw;
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
