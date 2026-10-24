using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using QuranAnchor.Domain.Entities;
using QuranAnchor.Infrastructure.Identity;

namespace QuranAnchor.Infrastructure.Persistence;

public class QuranAnchorDbContext(DbContextOptions<QuranAnchorDbContext> options)
    : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>(options)
{
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<AppSetting> AppSettings => Set<AppSetting>();

    public DbSet<StudentProfile> Students => Set<StudentProfile>();
    public DbSet<TutorProfile> Tutors => Set<TutorProfile>();
    public DbSet<TutorAssignment> TutorAssignments => Set<TutorAssignment>();

    public DbSet<ClassSchedule> Schedules => Set<ClassSchedule>();
    public DbSet<SessionPackage> Packages => Set<SessionPackage>();
    public DbSet<ClassSession> Sessions => Set<ClassSession>();
    public DbSet<RescheduleRequest> RescheduleRequests => Set<RescheduleRequest>();
    public DbSet<BreakDay> BreakDays => Set<BreakDay>();
    public DbSet<SentNotification> SentNotifications => Set<SentNotification>();
    public DbSet<IdCounter> IdCounters => Set<IdCounter>();

    public DbSet<SessionFeedback> Feedback => Set<SessionFeedback>();
    public DbSet<MonthlyPlan> Plans => Set<MonthlyPlan>();
    public DbSet<PlanRow> PlanRows => Set<PlanRow>();
    public DbSet<MonthlyReport> Reports => Set<MonthlyReport>();

    public DbSet<PortalPrice> PortalPrices => Set<PortalPrice>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<SessionRate> SessionRates => Set<SessionRate>();
    public DbSet<Announcement> Announcements => Set<Announcement>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // utf8mb4 so Arabic text and emoji (the WhatsApp feedback format) are stored correctly.
        builder.UseCollation("utf8mb4_unicode_ci");

        builder.Entity<ApplicationUser>(e => e.Property(x => x.FullName).HasMaxLength(200));

        builder.Entity<AuditLog>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Action).HasMaxLength(100).IsRequired();
            e.Property(x => x.Target).HasMaxLength(200);
            e.Property(x => x.Details).HasMaxLength(2000);
            e.Property(x => x.UserId).HasMaxLength(64);
            e.Property(x => x.UserName).HasMaxLength(256);
            e.Property(x => x.IpAddress).HasMaxLength(64);
            e.HasIndex(x => x.OccurredAtUtc);
        });

        builder.Entity<AppSetting>(e =>
        {
            e.HasKey(x => x.Key);
            e.Property(x => x.Key).HasMaxLength(100);
            e.Property(x => x.Value).HasMaxLength(8000);
            e.Property(x => x.UpdatedBy).HasMaxLength(256);
        });

        builder.Entity<StudentProfile>(e =>
        {
            e.Property(x => x.StudentCode).HasMaxLength(40);
            e.HasIndex(x => x.StudentCode).IsUnique();
            e.HasIndex(x => x.UserId).IsUnique();
            e.Property(x => x.FullName).HasMaxLength(200);
            e.Property(x => x.ParentName).HasMaxLength(200);
            e.Property(x => x.ParentEmail).HasMaxLength(256);
            e.Property(x => x.Phone).HasMaxLength(40);
            e.Property(x => x.Country).HasMaxLength(80);
            e.Property(x => x.TimeZoneId).HasMaxLength(60);
            e.Property(x => x.MeetingLink).HasMaxLength(500);
            e.Property(x => x.PreferredCurrency).HasMaxLength(3);
            e.HasOne(x => x.Tutor).WithMany().HasForeignKey(x => x.TutorId).OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<TutorProfile>(e =>
        {
            e.Property(x => x.TutorCode).HasMaxLength(40);
            e.HasIndex(x => x.TutorCode).IsUnique();
            e.HasIndex(x => x.UserId).IsUnique();
            e.Property(x => x.FullName).HasMaxLength(200);
            e.Property(x => x.Email).HasMaxLength(256);
            e.Property(x => x.Phone).HasMaxLength(40);
            e.Property(x => x.TimeZoneId).HasMaxLength(60);
        });

        builder.Entity<TutorAssignment>(e =>
        {
            e.HasOne(x => x.Student).WithMany().HasForeignKey(x => x.StudentId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Tutor).WithMany().HasForeignKey(x => x.TutorId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ClassSchedule>(e =>
        {
            e.Property(x => x.TimeZoneId).HasMaxLength(60);
            e.HasOne(x => x.Student).WithMany().HasForeignKey(x => x.StudentId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Tutor).WithMany().HasForeignKey(x => x.TutorId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<SessionPackage>(e =>
        {
            e.Ignore(x => x.Total);
            e.HasIndex(x => new { x.StudentId, x.Year, x.Month }).IsUnique();
            e.HasOne(x => x.Student).WithMany().HasForeignKey(x => x.StudentId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<ClassSession>(e =>
        {
            e.Ignore(x => x.EndUtc);
            e.Property(x => x.NotHeldNote).HasMaxLength(500);
            e.Property(x => x.AmountEarned).HasPrecision(12, 2);
            e.HasIndex(x => new { x.TutorId, x.StartUtc });
            e.HasIndex(x => new { x.StudentId, x.StartUtc });
            e.HasOne(x => x.Student).WithMany().HasForeignKey(x => x.StudentId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Tutor).WithMany().HasForeignKey(x => x.TutorId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Package).WithMany().HasForeignKey(x => x.PackageId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Feedback).WithOne(f => f.Session).HasForeignKey<SessionFeedback>(f => f.SessionId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<RescheduleRequest>(e =>
        {
            e.Property(x => x.Reason).HasMaxLength(500);
            e.HasOne(x => x.Session).WithMany().HasForeignKey(x => x.SessionId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<BreakDay>(e => e.Property(x => x.Reason).HasMaxLength(200));

        builder.Entity<SentNotification>(e =>
        {
            e.Property(x => x.Key).HasMaxLength(150);
            e.HasIndex(x => x.Key).IsUnique();
        });

        builder.Entity<IdCounter>(e =>
        {
            e.HasKey(x => x.Key);
            e.Property(x => x.Key).HasMaxLength(20);
        });

        builder.Entity<SessionFeedback>(e =>
        {
            e.HasIndex(x => x.SessionId).IsUnique();
            e.Property(x => x.LessonTitle).HasMaxLength(300);
            e.Property(x => x.LearningObjective).HasMaxLength(500);
            e.Property(x => x.GoalNote).HasMaxLength(500);
            e.Property(x => x.OnTrackComment).HasMaxLength(500);
            e.Property(x => x.TaskComment).HasMaxLength(500);
            e.Property(x => x.TaskText).HasMaxLength(1000);
            e.Property(x => x.SlidesUrl).HasMaxLength(500);
            e.Property(x => x.Worksheet).HasMaxLength(500);
            e.Property(x => x.TeacherNote).HasMaxLength(3000);
        });

        builder.Entity<MonthlyPlan>(e =>
        {
            e.HasIndex(x => new { x.StudentId, x.Year, x.Month }).IsUnique();
            e.Property(x => x.Goal).HasMaxLength(500);
            e.Property(x => x.MonthGoals).HasMaxLength(2000);
            e.Property(x => x.PostClassExpectations).HasMaxLength(2000);
            e.Property(x => x.DailyTilawahFocus).HasMaxLength(2000);
            e.Property(x => x.ClassFormat).HasMaxLength(500);
            e.Property(x => x.ParentNotes).HasMaxLength(2000);
            e.HasOne(x => x.Student).WithMany().HasForeignKey(x => x.StudentId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Tutor).WithMany().HasForeignKey(x => x.TutorId).OnDelete(DeleteBehavior.Restrict);
            e.HasMany(x => x.Rows).WithOne(r => r.Plan).HasForeignKey(r => r.PlanId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<PlanRow>(e =>
        {
            e.Property(x => x.Content).HasMaxLength(600);
            e.Property(x => x.Activity).HasMaxLength(600);
            e.Property(x => x.Murajaah).HasMaxLength(600);
        });

        builder.Entity<MonthlyReport>(e =>
        {
            e.HasIndex(x => new { x.StudentId, x.Year, x.Month }).IsUnique();
            e.Property(x => x.TutorRemark).HasMaxLength(2000);
            e.Property(x => x.FocusNextMonth).HasMaxLength(2000);
            e.Property(x => x.ParentNote).HasMaxLength(2000);
            e.HasOne(x => x.Student).WithMany().HasForeignKey(x => x.StudentId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<PortalPrice>(e =>
        {
            e.Property(x => x.Currency).HasMaxLength(3);
            e.Property(x => x.Amount).HasPrecision(12, 2);
        });

        builder.Entity<Payment>(e =>
        {
            e.Property(x => x.Reference).HasMaxLength(100);
            e.HasIndex(x => x.Reference).IsUnique();
            e.Property(x => x.Currency).HasMaxLength(3);
            e.Property(x => x.Amount).HasPrecision(12, 2);
            e.Property(x => x.Provider).HasMaxLength(30);
            e.Property(x => x.Note).HasMaxLength(300);
            e.HasOne(x => x.Student).WithMany().HasForeignKey(x => x.StudentId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<SessionRate>(e => e.Property(x => x.Amount).HasPrecision(12, 2));

        builder.Entity<Announcement>(e =>
        {
            e.Property(x => x.Title).HasMaxLength(200);
            e.Property(x => x.Body).HasMaxLength(4000);
            e.Property(x => x.CreatedBy).HasMaxLength(200);
        });
    }
}
