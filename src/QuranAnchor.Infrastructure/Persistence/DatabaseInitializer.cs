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
    /// Applies migrations (when Database:AutoMigrate is true), makes sure the five roles exist,
    /// and creates the first Super Admin from the Seed:SuperAdmin settings if none exists yet.
    /// </summary>
    public static async Task InitializeAsync(IServiceProvider services, IConfiguration config)
    {
        using var scope = services.CreateScope();
        var sp = scope.ServiceProvider;

        var db = sp.GetRequiredService<QuranAnchorDbContext>();
        if (config.GetValue<bool>("Database:AutoMigrate"))
        {
            await db.Database.MigrateAsync();
        }

        var roleManager = sp.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        foreach (var role in Roles.All)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole<Guid>(role));
            }
        }

        // Starting prices and tutor rates from the PRD. The Super Admin can change them on the Pricing page.
        if (!await db.PortalPrices.AnyAsync())
        {
            db.PortalPrices.AddRange(
                new QuranAnchor.Domain.Entities.PortalPrice { Currency = "NGN", Amount = 2000m },
                new QuranAnchor.Domain.Entities.PortalPrice { Currency = "USD", Amount = 1.50m },
                new QuranAnchor.Domain.Entities.PortalPrice { Currency = "GBP", Amount = 1.15m });
        }
        if (!await db.SessionRates.AnyAsync())
        {
            db.SessionRates.AddRange(
                new QuranAnchor.Domain.Entities.SessionRate { DurationMinutes = 30, Amount = 850m },
                new QuranAnchor.Domain.Entities.SessionRate { DurationMinutes = 45, Amount = 1275m });
        }
        await db.SaveChangesAsync();

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

        await users.AddToRoleAsync(user, Roles.SuperAdmin);
    }
}
