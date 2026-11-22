using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using QuranAnchor.Domain.Entities;
using QuranAnchor.Infrastructure.Identity;

namespace QuranAnchor.Infrastructure.Persistence;

public class QuranAnchorDbContext(DbContextOptions<QuranAnchorDbContext> options, QuranAnchor.Application.Abstractions.ICurrentScope? scope = null)
    : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>(options)
{
    /// <summary>The supervisor whose view applies to this request. Empty for everyone else, so they see everything.</summary>
    private Guid? ScopeSupervisorId => scope?.SupervisorId;

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
    public DbSet<PlanGoal> PlanGoals => Set<PlanGoal>();
    public DbSet<MonthlyReport> Reports => Set<MonthlyReport>();

    public DbSet<PortalPrice> PortalPrices => Set<PortalPrice>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<SessionRate> SessionRates => Set<SessionRate>();
    public DbSet<Announcement> Announcements => Set<Announcement>();
    public DbSet<Certificate> Certificates => Set<Certificate>();
    public DbSet<Enquiry> Enquiries => Set<Enquiry>();
    public DbSet<Meeting> Meetings => Set<Meeting>();
    public DbSet<MeetingInvitee> MeetingInvitees => Set<MeetingInvitee>();
    public DbSet<TutorAvailability> Availability => Set<TutorAvailability>();
    public DbSet<TutorRating> TutorRatings => Set<TutorRating>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<InvoicePayment> InvoicePayments => Set<InvoicePayment>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // A supervisor only sees their own tutors and students, and the records that belong to them.
        builder.Entity<TutorProfile>().Property(x => x.AvailabilityNote).HasMaxLength(300);
        builder.Entity<StudentProfile>().HasQueryFilter(x => ScopeSupervisorId == null || x.SupervisorId == ScopeSupervisorId);
        builder.Entity<TutorProfile>().HasQueryFilter(x => ScopeSupervisorId == null || x.SupervisorId == ScopeSupervisorId);
        builder.Entity<ClassSchedule>().HasQueryFilter(x => ScopeSupervisorId == null || x.Student!.SupervisorId == ScopeSupervisorId);
        builder.Entity<SessionPackage>().HasQueryFilter(x => ScopeSupervisorId == null || x.Student!.SupervisorId == ScopeSupervisorId);
        builder.Entity<ClassSession>().HasQueryFilter(x => ScopeSupervisorId == null || x.Student!.SupervisorId == ScopeSupervisorId);
        builder.Entity<MonthlyPlan>().HasQueryFilter(x => ScopeSupervisorId == null || x.Student!.SupervisorId == ScopeSupervisorId);
        builder.Entity<MonthlyReport>().HasQueryFilter(x => ScopeSupervisorId == null || x.Student!.SupervisorId == ScopeSupervisorId);
        builder.Entity<Payment>().HasQueryFilter(x => ScopeSupervisorId == null || x.Student!.SupervisorId == ScopeSupervisorId);
        builder.Entity<Certificate>().HasQueryFilter(x => ScopeSupervisorId == null || x.Student!.SupervisorId == ScopeSupervisorId);
        builder.Entity<Invoice>().HasQueryFilter(x => ScopeSupervisorId == null || x.Student!.SupervisorId == ScopeSupervisorId);
        builder.Entity<TutorRating>().HasQueryFilter(x => ScopeSupervisorId == null || x.Student!.SupervisorId == ScopeSupervisorId);
        builder.Entity<TutorAssignment>().HasQueryFilter(x => ScopeSupervisorId == null || x.Student!.SupervisorId == ScopeSupervisorId);
        builder.Entity<TutorAvailability>().HasQueryFilter(x => ScopeSupervisorId == null || x.Tutor!.SupervisorId == ScopeSupervisorId);
        // An enquiry nobody has taken yet is visible to every supervisor. Once taken, only that supervisor sees it.
        builder.Entity<Enquiry>().HasQueryFilter(x => ScopeSupervisorId == null || x.SupervisorId == null || x.SupervisorId == ScopeSupervisorId);
        builder.Entity<Meeting>().HasQueryFilter(x => ScopeSupervisorId == null || x.SupervisorId == null || x.SupervisorId == ScopeSupervisorId);

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
            e.Property(x => x.ClassFeeAmount).HasPrecision(12, 2);
            e.Property(x => x.ClassFeeCurrency).HasMaxLength(3).HasDefaultValue("NGN");
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
            e.Property(x => x.PostClassExpectations).HasMaxLength(2000);
            e.Property(x => x.ParentNotes).HasMaxLength(2000);
            e.HasOne(x => x.Student).WithMany().HasForeignKey(x => x.StudentId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Tutor).WithMany().HasForeignKey(x => x.TutorId).OnDelete(DeleteBehavior.Restrict);
            e.HasMany(x => x.Goals).WithOne(g => g.Plan).HasForeignKey(g => g.PlanId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<PlanGoal>(e =>
        {
            e.Property(x => x.Text).HasMaxLength(200);
            e.HasIndex(x => new { x.PlanId, x.Week, x.Track }).IsUnique();
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

        builder.Entity<Meeting>(e =>
        {
            e.Property(x => x.Title).HasMaxLength(200);
            e.Property(x => x.Agenda).HasMaxLength(4000);
            e.Property(x => x.Notes).HasMaxLength(4000);
            e.Property(x => x.MeetingLink).HasMaxLength(500);
            e.Property(x => x.TimeZoneId).HasMaxLength(60);
            e.Property(x => x.CreatedBy).HasMaxLength(200);
            e.HasIndex(x => x.StartUtc);
            e.HasMany(x => x.Invitees).WithOne(i => i.Meeting).HasForeignKey(i => i.MeetingId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<MeetingInvitee>(e =>
        {
            e.HasIndex(x => new { x.MeetingId, x.TutorId }).IsUnique();
            e.HasOne(x => x.Tutor).WithMany().HasForeignKey(x => x.TutorId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<Enquiry>(e =>
        {
            e.Property(x => x.ParentName).HasMaxLength(200);
            e.Property(x => x.Email).HasMaxLength(256);
            e.Property(x => x.Phone).HasMaxLength(40);
            e.Property(x => x.StudentName).HasMaxLength(200);
            e.Property(x => x.Country).HasMaxLength(80);
            e.Property(x => x.TimeZoneId).HasMaxLength(60);
            e.Property(x => x.PreferredTimes).HasMaxLength(500);
            e.Property(x => x.Message).HasMaxLength(2000);
            e.Property(x => x.Notes).HasMaxLength(2000);
            e.Property(x => x.TrialMeetingLink).HasMaxLength(500);
            e.HasIndex(x => x.CreatedAtUtc);
        });

        builder.Entity<TutorAvailability>(e =>
        {
            e.Property(x => x.Note).HasMaxLength(200);
            e.HasOne(x => x.Tutor).WithMany().HasForeignKey(x => x.TutorId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<TutorRating>(e =>
        {
            e.Property(x => x.Comment).HasMaxLength(1000);
            e.HasIndex(x => new { x.StudentId, x.Year, x.Month }).IsUnique();
            e.HasOne(x => x.Student).WithMany().HasForeignKey(x => x.StudentId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Tutor).WithMany().HasForeignKey(x => x.TutorId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<Invoice>(e =>
        {
            e.Ignore(x => x.PaidAmount);
            e.Ignore(x => x.Balance);
            e.Property(x => x.Number).HasMaxLength(40);
            e.HasIndex(x => x.Number).IsUnique();
            e.Property(x => x.Description).HasMaxLength(300);
            e.Property(x => x.Currency).HasMaxLength(3);
            e.Property(x => x.Amount).HasPrecision(12, 2);
            e.HasOne(x => x.Student).WithMany().HasForeignKey(x => x.StudentId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.Payments).WithOne(p => p.Invoice).HasForeignKey(p => p.InvoiceId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<InvoicePayment>(e =>
        {
            e.Property(x => x.Amount).HasPrecision(12, 2);
            e.Property(x => x.Method).HasMaxLength(60);
            e.Property(x => x.Reference).HasMaxLength(100);
            e.Property(x => x.RecordedBy).HasMaxLength(200);
        });

        builder.Entity<Certificate>(e =>
        {
            e.Property(x => x.Code).HasMaxLength(40);
            e.HasIndex(x => x.Code).IsUnique();
            e.Property(x => x.Title).HasMaxLength(200);
            e.Property(x => x.Description).HasMaxLength(1000);
            e.Property(x => x.IssuedByName).HasMaxLength(200);
            e.HasOne(x => x.Student).WithMany().HasForeignKey(x => x.StudentId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<Announcement>(e =>
        {
            e.Property(x => x.Title).HasMaxLength(200);
            e.Property(x => x.Body).HasMaxLength(4000);
            e.Property(x => x.CreatedBy).HasMaxLength(200);
            e.Property(x => x.UpdatedBy).HasMaxLength(200);
            e.Property(x => x.FlyerPath).HasMaxLength(300);
            e.Property(x => x.FlyerName).HasMaxLength(150);
            e.Property(x => x.FlyerKind).HasMaxLength(10);
        });
    }
}
