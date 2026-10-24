using QuranAnchor.Domain.Constants;

namespace QuranAnchor.Application.Security;

/// <summary>
/// Which permissions each role holds. Super Admin can change who holds a role,
/// but the list of permissions itself lives in code so it can be tested.
/// </summary>
public static class RolePermissions
{
    private static readonly Dictionary<string, HashSet<string>> Map = new()
    {
        [Roles.SuperAdmin] = new HashSet<string>(Permissions.All),

        [Roles.Admin] = new HashSet<string>
        {
            Permissions.CreateAccounts,
            Permissions.EditAccounts,
            Permissions.DeactivateAccounts,
            Permissions.ReactivateAccounts,
            Permissions.ManageStaff,
            Permissions.ViewAllStudents,
            Permissions.ReplaceTutor,
            Permissions.RotateMeetLink,
            Permissions.ManageSchedules,
            Permissions.ManageBreakDays,
            Permissions.OverrideReschedules,
            Permissions.ViewMissingFeedback,
            Permissions.ViewPayments,
            Permissions.MarkFeePaid,
            Permissions.ViewAllEarnings,
            Permissions.PostAnnouncements
        },

        [Roles.Supervisor] = new HashSet<string>
        {
            Permissions.CreateAccounts,
            Permissions.EditAccounts,
            Permissions.DeactivateAccounts,
            Permissions.ReactivateAccounts,
            Permissions.ViewAllStudents,
            Permissions.ReplaceTutor,
            Permissions.RotateMeetLink,
            Permissions.ManageSchedules,
            Permissions.OverrideReschedules,
            Permissions.ViewMissingFeedback,
            Permissions.ViewPayments,
            Permissions.MarkFeePaid,
            Permissions.ViewAllEarnings,
            Permissions.PostAnnouncements
        },

        [Roles.Tutor] = new HashSet<string>(),
        [Roles.Student] = new HashSet<string>()
    };

    public static bool Has(string role, string permission) =>
        Map.TryGetValue(role, out var set) && set.Contains(permission);
}
