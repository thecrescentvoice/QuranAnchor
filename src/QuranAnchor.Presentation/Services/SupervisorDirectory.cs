using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Rendering;
using QuranAnchor.Domain.Constants;
using QuranAnchor.Infrastructure.Identity;

namespace QuranAnchor.Presentation.Services;

/// <summary>The supervisors (staff users), for choosing who is in charge of a tutor, a student or an enquiry.</summary>
public sealed class SupervisorDirectory(UserManager<ApplicationUser> users)
{
    private List<ApplicationUser>? _all;

    public async Task<List<ApplicationUser>> AllAsync() =>
        _all ??= (await users.GetUsersInRoleAsync(Roles.Supervisor)).OrderBy(u => u.FullName).ToList();

    public async Task<List<SelectListItem>> OptionsAsync() =>
        (await AllAsync()).Where(u => u.IsActive).Select(u => new SelectListItem(u.FullName, u.Id.ToString())).ToList();

    public async Task<string> NameAsync(Guid? id) =>
        id is null ? "Not assigned" : (await AllAsync()).FirstOrDefault(u => u.Id == id)?.FullName ?? "Unknown";
}
