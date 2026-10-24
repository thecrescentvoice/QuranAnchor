using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using QuranAnchor.Domain.Constants;

namespace QuranAnchor.Presentation.Filters;

/// <summary>
/// Sends people to finish account setup before anything else:
/// 1) choose their own password, 2) set up two-step verification (Super Admin and Admin), 3) accept the privacy notice (parents).
/// </summary>
public sealed class AccountSetupFilter : IActionFilter
{
    public void OnActionExecuting(ActionExecutingContext context)
    {
        var user = context.HttpContext.User;
        if (user.Identity?.IsAuthenticated != true) return;

        var controller = context.RouteData.Values["controller"]?.ToString();
        var action = context.RouteData.Values["action"]?.ToString();
        bool On(string name) => controller == "Account" && action == name;

        if (On("Logout")) return;

        if (user.HasClaim(AppClaimTypes.MustChangePassword, "1"))
        {
            if (!On("ChangePassword")) context.Result = new RedirectToActionResult("ChangePassword", "Account", null);
            return;
        }
        if (user.HasClaim(AppClaimTypes.TwoFactorRequired, "1"))
        {
            if (!On("TwoFactorSetup")) context.Result = new RedirectToActionResult("TwoFactorSetup", "Account", null);
            return;
        }
        if (user.HasClaim(AppClaimTypes.ConsentRequired, "1"))
        {
            if (!On("Consent")) context.Result = new RedirectToActionResult("Consent", "Account", null);
        }
    }

    public void OnActionExecuted(ActionExecutedContext context) { }
}
