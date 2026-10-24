using Microsoft.AspNetCore.Authorization;
using QuranAnchor.Application.Security;
using QuranAnchor.Domain.Constants;

namespace QuranAnchor.Presentation.Authorization;

/// <summary>"The signed-in person must hold this permission through one of their roles."</summary>
public sealed class PermissionRequirement(string permission) : IAuthorizationRequirement
{
    public string Permission { get; } = permission;
}

public sealed class PermissionHandler : AuthorizationHandler<PermissionRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        foreach (var role in Roles.All)
        {
            if (context.User.IsInRole(role) && RolePermissions.Has(role, requirement.Permission))
            {
                context.Succeed(requirement);
                break;
            }
        }

        return Task.CompletedTask;
    }
}

public static class AuthorizationExtensions
{
    /// <summary>Creates one policy per permission, named exactly like the permission.</summary>
    public static IServiceCollection AddPermissionPolicies(this IServiceCollection services)
    {
        services.AddSingleton<IAuthorizationHandler, PermissionHandler>();
        services.AddAuthorization(options =>
        {
            foreach (var permission in Permissions.All)
            {
                options.AddPolicy(permission, policy =>
                    policy.RequireAuthenticatedUser().AddRequirements(new PermissionRequirement(permission)));
            }
        });
        return services;
    }
}
