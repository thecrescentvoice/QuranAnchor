using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using QuranAnchor.Application.Abstractions;
using QuranAnchor.Domain.Constants;
using QuranAnchor.Infrastructure.Identity;
using QuranAnchor.Presentation.Services;
using QuranAnchor.Presentation.ViewModels;

namespace QuranAnchor.Presentation.Controllers;

/// <summary>Admins and supervisors. You can only manage roles below your own.</summary>
[Authorize(Policy = Permissions.ManageStaff)]
public class StaffController(
    UserManager<ApplicationUser> users,
    IAccountService accounts,
    IAuditLogger audit,
    CurrentUser me) : Controller
{
    public async Task<IActionResult> Index()
    {
        var list = new List<(ApplicationUser User, string Role)>();
        foreach (var role in new[] { Roles.Admin, Roles.Supervisor })
        {
            if (!Roles.CanManage(me.Role, role)) continue;
            foreach (var u in await users.GetUsersInRoleAsync(role)) list.Add((u, role));
        }
        return View(list.OrderBy(x => x.Role).ThenBy(x => x.User.FullName).ToList());
    }

    [HttpGet]
    public IActionResult Create() => View(new StaffFormViewModel());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(StaffFormViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var result = await accounts.CreateStaffAsync(model.FullName, model.Email, model.Role, me.Role);
        if (!result.Ok)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "The account could not be created.");
            return View(model);
        }

        await audit.LogAsync("StaffCreated", model.Email, model.Role);
        TempData["Saved"] = $"Account created. Temporary password: {result.TempPassword}. It was also emailed to {model.Email}.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SetActive(Guid id, bool active)
    {
        var result = await accounts.SetActiveAsync(id, active, me.Role);
        await audit.LogAsync(active ? "StaffReactivated" : "StaffDeactivated", id.ToString());
        TempData[result.Ok ? "Saved" : "Warning"] = result.Ok ? "Saved." : result.Error;
        return RedirectToAction(nameof(Index));
    }
}
