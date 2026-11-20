using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using QuranAnchor.Domain.Constants;
using QuranAnchor.Infrastructure.Identity;

namespace QuranAnchor.Infrastructure.Persistence;

public static class DatabaseInitializer
{
    /// <summary>
    /// Applies migrations only when explicitly enabled, initializes roles and default pricing,
    /// and optionally creates the first Super Admin from secret configuration.
    /// </summary>
    public static async Task InitializeAsync(
        IServiceProvider services,
        IConfiguration config,
        CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var sp = scope.ServiceProvider;

        var db = sp.GetRequiredService<QuranAnchorDbContext>();
        if (config.GetValue<bool>("Database:AutoMigrate"))
        {
            await db.Database.MigrateAsync(cancellationToken);
        }
        else
        {
            // Fail early with an explicit instruction rather than surfacing a later missing-table
            // error from Identity, seeding or a request. Do not use EnsureCreated: it bypasses migration history.
            var pendingMigrations = await db.Database.GetPendingMigrationsAsync(cancellationToken);
            var pending = pendingMigrations.ToArray();
            if (pending.Length > 0)
            {
                throw new InvalidOperationException(
                    $"The database has {pending.Length} unapplied EF Core migration(s), including '{pending[0]}'. " +
                    "Apply reviewed migrations before starting, or set Database:AutoMigrate=true after taking a database backup.");
            }
        }

        var roleManager = sp.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        foreach (var role in Roles.All)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                var result = await roleManager.CreateAsync(new IdentityRole<Guid>(role));
                if (!result.Succeeded)
                {
                    throw new InvalidOperationException(
                        $"Could not initialize the required '{role}' role: " +
                        string.Join("; ", result.Errors.Select(e => e.Description)));
                }
            }
        }

        // Starting prices and tutor rates from the PRD. The Super Admin can change them on the Pricing page.
        if (!await db.PortalPrices.AnyAsync(cancellationToken))
        {
            db.PortalPrices.AddRange(
                new QuranAnchor.Domain.Entities.PortalPrice { Currency = "NGN", Amount = 2000m },
                new QuranAnchor.Domain.Entities.PortalPrice { Currency = "USD", Amount = 1.50m },
                new QuranAnchor.Domain.Entities.PortalPrice { Currency = "GBP", Amount = 1.15m });
        }
        if (!await db.SessionRates.AnyAsync(cancellationToken))
        {
            db.SessionRates.AddRange(
                new QuranAnchor.Domain.Entities.SessionRate { DurationMinutes = 30, Amount = 850m },
                new QuranAnchor.Domain.Entities.SessionRate { DurationMinutes = 45, Amount = 1275m });
        }
        await db.SaveChangesAsync(cancellationToken);

        var email = config["Seed:SuperAdmin:Email"];
        var password = config["Seed:SuperAdmin:Password"];
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            return;
        }

        var users = sp.GetRequiredService<UserManager<ApplicationUser>>();
        if ((await users.GetUsersInRoleAsync(Roles.SuperAdmin)).Count > 0)
        {
            return;
        }

        var fullName = config["Seed:SuperAdmin:FullName"];
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = email.Trim(),
            Email = email.Trim(),
            EmailConfirmed = true,
            FullName = string.IsNullOrWhiteSpace(fullName) ? "Super Admin" : fullName.Trim(),
            IsActive = true,
            MustChangePassword = true,
            CreatedAtUtc = DateTime.UtcNow
        };

        var created = await users.CreateAsync(user, password);
        if (!created.Succeeded)
        {
            throw new InvalidOperationException(
                "Could not create the first Super Admin: " +
                string.Join("; ", created.Errors.Select(e => e.Description)));
        }

        var addedToRole = await users.AddToRoleAsync(user, Roles.SuperAdmin);
        if (!addedToRole.Succeeded)
        {
            // Avoid leaving a valid account with no administrative role; cleanup is best-effort,
            // while the role-assignment failure remains the startup error.
            await users.DeleteAsync(user);
            throw new InvalidOperationException(
                "Could not assign the Super Admin role to the initial account: " +
                string.Join("; ", addedToRole.Errors.Select(e => e.Description)));
        }
    }
}
