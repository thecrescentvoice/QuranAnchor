namespace QuranAnchor.Domain.Constants;

/// <summary>
/// Everything a role may be allowed to do. Roles are mapped to permissions in
/// QuranAnchor.Application.Security.RolePermissions. Each permission is also an
/// authorization policy name, so a controller can use [Authorize(Policy = Permissions.X)].
/// </summary>
public static class Permissions
{
    public const string ViewAuditLog = "audit.view";
    public const string EditBranding = "settings.branding.edit";
    public const string EditSystemSettings = "settings.system.edit";

    public const string CreateAccounts = "accounts.create";
    public const string EditAccounts = "accounts.edit";
    public const string DeactivateAccounts = "accounts.deactivate";
    public const string ReactivateAccounts = "accounts.reactivate";
    public const string DeleteAccounts = "accounts.delete";
    public const string ManageStaff = "accounts.manage-staff";

    public const string ViewAllStudents = "students.view-all";
    public const string ReplaceTutor = "tutors.replace";
    public const string RotateMeetLink = "students.rotate-link";
    public const string ManageSchedules = "schedules.manage";
    public const string ManageBreakDays = "breaks.manage";
    public const string OverrideReschedules = "reschedules.override";
    public const string ViewMissingFeedback = "feedback.view-missing";
    public const string ViewPayments = "payments.view";
    public const string MarkFeePaid = "payments.mark-paid";
    public const string EditPricing = "settings.pricing.edit";
    public const string ViewAllEarnings = "earnings.view-all";
    public const string PostAnnouncements = "announcements.post";

    public static readonly IReadOnlyList<string> All = new[]
    {
        ViewAuditLog, EditBranding, EditSystemSettings,
        CreateAccounts, EditAccounts, DeactivateAccounts,
        ReactivateAccounts, DeleteAccounts, ManageStaff,
        ViewAllStudents, ReplaceTutor, RotateMeetLink, ManageSchedules, ManageBreakDays,
        OverrideReschedules, ViewMissingFeedback, ViewPayments, MarkFeePaid, EditPricing,
        ViewAllEarnings, PostAnnouncements
    };
}
