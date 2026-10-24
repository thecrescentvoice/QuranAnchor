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
        var connectionString = config.GetConnectionString("Default")
            ?? throw new InvalidOperationException("The connection string 'Default' is missing.");

        // If your server version is not 8.0.36, set Database:ServerVersion (for example "8.4.0-mysql").
        // Leave it empty to detect it automatically (needs the database to be running).
        var versionText = config["Database:ServerVersion"];
        var serverVersion = string.IsNullOrWhiteSpace(versionText)
            ? ServerVersion.AutoDetect(connectionString)
            : ServerVersion.Parse(versionText);

        // The only place that names the MySQL provider, so it is easy to swap if needed.
        services.AddDbContext<QuranAnchorDbContext>(options =>
            options.UseMySql(connectionString, serverVersion, mysql =>
                mysql.MigrationsAssembly(typeof(QuranAnchorDbContext).Assembly.GetName().Name)));

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
