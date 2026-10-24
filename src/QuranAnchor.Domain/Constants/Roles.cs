namespace QuranAnchor.Domain.Constants;

/// <summary>The five roles in the portal, ordered from most to least powerful.</summary>
public static class Roles
{
    public const string SuperAdmin = "SuperAdmin";
    public const string Admin = "Admin";
    public const string Supervisor = "Supervisor";
    public const string Tutor = "Tutor";
    public const string Student = "Student";

    public static readonly IReadOnlyList<string> All =
        new[] { SuperAdmin, Admin, Supervisor, Tutor, Student };

    public static int Rank(string role) => role switch
    {
        SuperAdmin => 5,
        Admin => 4,
        Supervisor => 3,
        Tutor => 2,
        Student => 1,
        _ => 0
    };

    /// <summary>A person can only manage roles below their own.</summary>
    public static bool CanManage(string actorRole, string targetRole) =>
        Rank(actorRole) > Rank(targetRole);

    public static string DisplayName(string role) => role switch
    {
        SuperAdmin => "Super Admin",
        Admin => "Admin",
        Supervisor => "Supervisor",
        Tutor => "Tutor",
        Student => "Student",
        _ => role
    };
}
