using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using QuranAnchor.Domain.Constants;

namespace QuranAnchor.Infrastructure.Identity;

/// <summary>
/// Puts the person's name and any "still to do" flags into the sign-in cookie:
/// choose a password, accept the privacy notice (parents), set up two-step verification (Super Admin and Admin).
/// </summary>
public class AppClaimsPrincipalFactory(
    UserManager<ApplicationUser> userManager,
    RoleManager<IdentityRole<Guid>> roleManager,
    IOptions<IdentityOptions> options)
    : UserClaimsPrincipalFactory<ApplicationUser, IdentityRole<Guid>>(userManager, roleManager, options)
{
    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(ApplicationUser user)
    {
        var identity = await base.GenerateClaimsAsync(user);
        identity.AddClaim(new Claim(AppClaimTypes.FullName, user.FullName ?? string.Empty));

        if (user.MustChangePassword)
            identity.AddClaim(new Claim(AppClaimTypes.MustChangePassword, "1"));

        var roles = await UserManager.GetRolesAsync(user);
        if (roles.Contains(Roles.Student) && (user.ConsentVersion ?? 0) < PrivacyNotice.CurrentVersion)
            identity.AddClaim(new Claim(AppClaimTypes.ConsentRequired, "1"));

        if ((roles.Contains(Roles.SuperAdmin) || roles.Contains(Roles.Admin)) && !user.TwoFactorEnabled)
            identity.AddClaim(new Claim(AppClaimTypes.TwoFactorRequired, "1"));

        return identity;
    }
}
