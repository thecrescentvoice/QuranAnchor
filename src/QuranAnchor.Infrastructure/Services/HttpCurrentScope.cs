using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using QuranAnchor.Application.Abstractions;
using QuranAnchor.Domain.Constants;

namespace QuranAnchor.Infrastructure.Services;

/// <summary>The scope of the person making the request. Only a signed-in supervisor is limited to their own people.</summary>
public sealed class HttpCurrentScope(IHttpContextAccessor accessor) : ICurrentScope
{
    public Guid? SupervisorId
    {
        get
        {
            var user = accessor.HttpContext?.User;
            if (user?.Identity?.IsAuthenticated != true || !user.IsInRole(Roles.Supervisor)) return null;
            return Guid.TryParse(user.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : null;
        }
    }
}
