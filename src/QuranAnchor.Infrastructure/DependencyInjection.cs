using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using QuranAnchor.Application.Abstractions;
using QuranAnchor.Infrastructure.Email;
using QuranAnchor.Infrastructure.Identity;
using QuranAnchor.Infrastructure.Persistence;
using QuranAnchor.Infrastructure.Services;

namespace QuranAnchor.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        var connectionString = config.GetConnectionString("Default");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "The database connection string is not configured. Set ConnectionStrings__Default in the deployment environment.");
        }

        // Keep server detection off the startup path: Railway's database may not yet be reachable
        // when services are registered. Supply the actual MySQL version with Database:ServerVersion.
        var versionText = config["Database:ServerVersion"];
        if (string.IsNullOrWhiteSpace(versionText))
        {
            throw new InvalidOperationException(
                "Database:ServerVersion is not configured. Set it to the Railway MySQL version (for example 8.0.36-mysql).");
        }
        var serverVersion = ServerVersion.Parse(versionText);

        services.AddDbContext<QuranAnchorDbContext>(options =>
            options.UseMySql(connectionString, serverVersion, mysql =>
                mysql.MigrationsAssembly(typeof(QuranAnchorDbContext).Assembly.GetName().Name))
                .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.CoreEventId.PossibleIncorrectRequiredNavigationWithQueryFilterInteractionWarning)));

        services
            .AddIdentity<ApplicationUser, IdentityRole<Guid>>(options =>
            {
                options.Password.RequiredLength = 8;
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = false;
                options.Password.RequireUppercase = false;
                options.Password.RequireNonAlphanumeric = false;

                // Siblings share the parent's email, so email cannot be unique.
                options.User.RequireUniqueEmail = false;
                options.User.AllowedUserNameCharacters =
                    "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-._@+";

                options.Lockout.AllowedForNewUsers = true;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);

                options.SignIn.RequireConfirmedAccount = false;
            })
            .AddEntityFrameworkStores<QuranAnchorDbContext>()
            .AddDefaultTokenProviders()
            .AddClaimsPrincipalFactory<AppClaimsPrincipalFactory>();

        // Password reset links work once and expire after an hour.
        services.Configure<DataProtectionTokenProviderOptions>(o => o.TokenLifespan = TimeSpan.FromHours(1));

        // A deactivated or changed account is signed out within about a minute.
        services.Configure<SecurityStampValidatorOptions>(o => o.ValidationInterval = TimeSpan.FromMinutes(1));

        services.AddMemoryCache();
        services.AddHostedService<DatabaseInitializerHostedService>();
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentScope, HttpCurrentScope>();

        services.AddScoped<IAuditLogger, AuditLogger>();
        services.AddScoped<ISettingsService, SettingsService>();

        services.AddScoped<IIdGenerator, IdGenerator>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<ISchedulingService, SchedulingService>();
        services.AddScoped<IFeedbackService, FeedbackService>();
        services.AddScoped<IReportService, ReportService>();
        services.AddScoped<IPdfService, PdfService>();
        services.AddScoped<IAccountService, AccountService>();
        services.AddHttpClient<IPaymentService, PaymentService>(client =>
            client.BaseAddress = new Uri("https://api.paystack.co/"));
        services.AddHostedService<ScheduledJobsService>();

        services.Configure<EmailOptions>(config.GetSection("Email"));
        var provider = config["Email:Provider"] ?? "Logging";
        if (provider.Equals("Resend", StringComparison.OrdinalIgnoreCase))
        {
            services.AddHttpClient<IEmailSender, ResendEmailSender>();
        }
        else
        {
            services.AddSingleton<IEmailSender, LoggingEmailSender>();
        }

        return services;
    }
}
