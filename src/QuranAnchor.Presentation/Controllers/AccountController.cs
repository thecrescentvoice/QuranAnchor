using System.Net;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using QuranAnchor.Application.Abstractions;
using QuranAnchor.Application.Models;
using QuranAnchor.Infrastructure.Identity;
using QuranAnchor.Presentation.ViewModels;

namespace QuranAnchor.Presentation.Controllers;

public class AccountController(
    UserManager<ApplicationUser> users,
    SignInManager<ApplicationUser> signIn,
    IEmailSender email,
    ISettingsService settings,
    IAuditLogger audit) : Controller
{
    private const string GenericLoginError =
        "We could not sign you in. Check your Student ID or email, and your password.";

    // ---------- Sign in / out ----------

    [HttpGet, AllowAnonymous]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction("Index", "Home");
        }

        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [HttpPost, AllowAnonymous, ValidateAntiForgeryToken, EnableRateLimiting("auth")]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var identifier = model.Identifier.Trim();

        // Students sign in with their Student ID, staff with their email. Both are stored as the UserName.
        var user = await users.FindByNameAsync(identifier);
        if (user is null || !user.IsActive)
        {
            await audit.LogAsync("LoginFailed", identifier, user is null ? "Unknown account" : "Inactive account");
            ModelState.AddModelError(string.Empty, GenericLoginError);
            return View(model);
        }

        var result = await signIn.PasswordSignInAsync(user, model.Password, isPersistent: false, lockoutOnFailure: true);

        if (result.Succeeded)
        {
            await audit.LogAsync("LoginSucceeded", user.UserName);

            if (user.MustChangePassword)
            {
                return RedirectToAction(nameof(ChangePassword));
            }

            if (!string.IsNullOrEmpty(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
            {
                return LocalRedirect(model.ReturnUrl);
            }

            return RedirectToAction("Index", "Home");
        }

        if (result.RequiresTwoFactor)
        {
            return RedirectToAction(nameof(LoginWith2fa), new { returnUrl = model.ReturnUrl });
        }

        if (result.IsLockedOut)
        {
            await audit.LogAsync("AccountLockedOut", user.UserName);
            ModelState.AddModelError(string.Empty,
                "Too many wrong attempts. Wait 15 minutes and try again, or use Forgot password.");
            return View(model);
        }

        await audit.LogAsync("LoginFailed", user.UserName, "Wrong password");
        ModelState.AddModelError(string.Empty, GenericLoginError);
        return View(model);
    }

    [HttpPost, Authorize, ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await audit.LogAsync("Logout");
        await signIn.SignOutAsync();
        return RedirectToAction(nameof(Login));
    }

    // ---------- Forgot / reset password ----------

    [HttpGet, AllowAnonymous]
    public IActionResult ForgotPassword() => View(new ForgotPasswordViewModel());

    [HttpPost, AllowAnonymous, ValidateAntiForgeryToken, EnableRateLimiting("auth")]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var input = model.Identifier.Trim();
        var normalizedName = users.NormalizeName(input);
        var normalizedEmail = users.NormalizeEmail(input);

        // A Student ID matches one account. A parent's email can match several (siblings).
        var matches = await users.Users
            .Where(u => u.IsActive && u.Email != null &&
                        (u.NormalizedUserName == normalizedName || u.NormalizedEmail == normalizedEmail))
            .ToListAsync();

        var branding = await settings.GetBrandingAsync();

        foreach (var group in matches.GroupBy(u => u.Email!, StringComparer.OrdinalIgnoreCase))
        {
            var lines = new List<string>();
            foreach (var account in group)
            {
                var token = await users.GeneratePasswordResetTokenAsync(account);
                var encoded = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
                var link = Url.Action(nameof(ResetPassword), "Account",
                    new { userId = account.Id, token = encoded }, Request.Scheme)!;

                var who = string.IsNullOrWhiteSpace(account.FullName)
                    ? account.UserName
                    : $"{account.FullName} ({account.UserName})";
                lines.Add($"<li><a href=\"{link}\">Reset the password for {WebUtility.HtmlEncode(who)}</a></li>");
            }

            await email.SendAsync(group.Key,
                $"Reset your {branding.ShortName} password",
                BuildResetEmail(branding, lines));
        }

        await audit.LogAsync("PasswordResetRequested", input);

        // The same answer whether or not an account was found, so nobody can check who has an account.
        return RedirectToAction(nameof(ForgotPasswordConfirmation));
    }

    [HttpGet, AllowAnonymous]
    public IActionResult ForgotPasswordConfirmation() => View();

    [HttpGet, AllowAnonymous]
    public IActionResult ResetPassword(Guid userId, string token) =>
        View(new ResetPasswordViewModel { UserId = userId, Token = token });

    [HttpPost, AllowAnonymous, ValidateAntiForgeryToken, EnableRateLimiting("auth")]
    public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        const string invalidLink = "This reset link is not valid any more. Ask for a new one.";

        var user = await users.FindByIdAsync(model.UserId.ToString());
        if (user is null || !user.IsActive)
        {
            ModelState.AddModelError(string.Empty, invalidLink);
            return View(model);
        }

        string rawToken;
        try
        {
            rawToken = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(model.Token));
        }
        catch (FormatException)
        {
            ModelState.AddModelError(string.Empty, invalidLink);
            return View(model);
        }

        var result = await users.ResetPasswordAsync(user, rawToken, model.NewPassword);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty,
                    error.Code == "InvalidToken" ? invalidLink : error.Description);
            }
            return View(model);
        }

        user.MustChangePassword = false;
        await users.UpdateAsync(user);
        await users.SetLockoutEndDateAsync(user, null);
        await users.ResetAccessFailedCountAsync(user);

        await audit.LogAsync("PasswordReset", user.UserName);
        return RedirectToAction(nameof(ResetPasswordConfirmation));
    }

    [HttpGet, AllowAnonymous]
    public IActionResult ResetPasswordConfirmation() => View();

    // ---------- Change password (also used for the first-login temporary password) ----------

    [HttpGet, Authorize]
    public IActionResult ChangePassword() =>
        View(new ChangePasswordViewModel
        {
            IsForced = User.HasClaim(QuranAnchor.Domain.Constants.AppClaimTypes.MustChangePassword, "1")
        });

    [HttpPost, Authorize, ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
    {
        model.IsForced = User.HasClaim(QuranAnchor.Domain.Constants.AppClaimTypes.MustChangePassword, "1");
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var user = await users.GetUserAsync(User);
        if (user is null)
        {
            return RedirectToAction(nameof(Login));
        }

        var result = await users.ChangePasswordAsync(user, model.CurrentPassword, model.NewPassword);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty,
                    error.Code == "PasswordMismatch" ? "Your current password is not correct." : error.Description);
            }
            return View(model);
        }

        user.MustChangePassword = false;
        await users.UpdateAsync(user);
        await signIn.RefreshSignInAsync(user);   // issues a fresh cookie without the "must change" flag

        await audit.LogAsync("PasswordChanged", user.UserName);
        TempData["Saved"] = "Your password has been changed.";
        return RedirectToAction("Index", "Home");
    }

    [HttpGet]
    public IActionResult AccessDenied() => View();

    // ---------- Two-step verification ----------

    [HttpGet, AllowAnonymous]
    public async Task<IActionResult> LoginWith2fa(string? returnUrl = null)
    {
        if (await signIn.GetTwoFactorAuthenticationUserAsync() is null) return RedirectToAction(nameof(Login));
        return View(new TwoFactorCodeViewModel { ReturnUrl = returnUrl });
    }

    [HttpPost, AllowAnonymous, ValidateAntiForgeryToken, EnableRateLimiting("auth")]
    public async Task<IActionResult> LoginWith2fa(TwoFactorCodeViewModel model)
    {
        if (!ModelState.IsValid) return View(model);
        var pending = await signIn.GetTwoFactorAuthenticationUserAsync();
        if (pending is null) return RedirectToAction(nameof(Login));

        var code = model.Code.Replace(" ", string.Empty).Replace("-", string.Empty);
        var result = await signIn.TwoFactorAuthenticatorSignInAsync(code, isPersistent: false, rememberClient: false);
        if (result.Succeeded)
        {
            await audit.LogAsync("LoginSucceeded", pending.UserName, "Two-step verification");
            if (!string.IsNullOrEmpty(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl)) return LocalRedirect(model.ReturnUrl);
            return RedirectToAction("Index", "Home");
        }

        ModelState.AddModelError(string.Empty, result.IsLockedOut
            ? "Too many wrong attempts. Wait 15 minutes and try again."
            : "That code is not correct. Check the code in your authenticator app and try again.");
        return View(model);
    }

    [HttpGet, Authorize]
    public async Task<IActionResult> TwoFactorSetup()
    {
        var user = await users.GetUserAsync(User);
        if (user is null) return RedirectToAction(nameof(Login));
        if (user.TwoFactorEnabled) return View(new TwoFactorSetupViewModel { Enabled = true });

        var key = await users.GetAuthenticatorKeyAsync(user);
        if (string.IsNullOrEmpty(key))
        {
            await users.ResetAuthenticatorKeyAsync(user);
            key = await users.GetAuthenticatorKeyAsync(user);
        }

        var branding = await settings.GetBrandingAsync();
        var issuer = Uri.EscapeDataString(branding.ShortName);
        var account = Uri.EscapeDataString(user.Email ?? user.UserName ?? "account");
        return View(new TwoFactorSetupViewModel
        {
            Key = string.Join(" ", Enumerable.Range(0, (key!.Length + 3) / 4).Select(i => key.Substring(i * 4, Math.Min(4, key.Length - i * 4)))).ToLowerInvariant(),
            Uri = $"otpauth://totp/{issuer}:{account}?secret={key}&issuer={issuer}&digits=6"
        });
    }

    [HttpPost, Authorize, ValidateAntiForgeryToken]
    public async Task<IActionResult> TwoFactorSetup(TwoFactorSetupViewModel model)
    {
        var user = await users.GetUserAsync(User);
        if (user is null) return RedirectToAction(nameof(Login));

        var code = (model.Code ?? string.Empty).Replace(" ", string.Empty).Replace("-", string.Empty);
        var valid = await users.VerifyTwoFactorTokenAsync(user, users.Options.Tokens.AuthenticatorTokenProvider, code);
        if (!valid)
        {
            TempData["Warning"] = "That code is not correct. Try the newest code in your app.";
            return RedirectToAction(nameof(TwoFactorSetup));
        }

        await users.SetTwoFactorEnabledAsync(user, true);
        await signIn.RefreshSignInAsync(user);
        await audit.LogAsync("TwoFactorEnabled", user.UserName);
        TempData["Saved"] = "Two-step verification is on. You will need a code each time you sign in.";
        return RedirectToAction("Index", "Home");
    }

    // ---------- Privacy notice ----------

    [HttpGet, Authorize]
    public IActionResult Consent() => View(new ConsentViewModel());

    [HttpPost, Authorize, ValidateAntiForgeryToken]
    public async Task<IActionResult> Consent(ConsentViewModel model)
    {
        if (!ModelState.IsValid) return View(model);
        var user = await users.GetUserAsync(User);
        if (user is null) return RedirectToAction(nameof(Login));

        user.ConsentVersion = QuranAnchor.Domain.Constants.PrivacyNotice.CurrentVersion;
        user.ConsentAtUtc = DateTime.UtcNow;
        await users.UpdateAsync(user);
        await signIn.RefreshSignInAsync(user);
        await audit.LogAsync("PrivacyNoticeAccepted", user.UserName, $"Version {user.ConsentVersion}");
        return RedirectToAction("Index", "Home");
    }

    // ---------- Helpers ----------

    private static string BuildResetEmail(BrandingSettings branding, IEnumerable<string> listItems)
    {
        var name = WebUtility.HtmlEncode(branding.AcademyName);
        return $"""
            <div style="font-family:Arial,Helvetica,sans-serif;font-size:16px;color:#2F3B3E;line-height:1.5">
              <p>Assalamu alaikum,</p>
              <p>We received a request to reset a password on {name}. Use the link below to choose a new one.
                 It works once and expires in one hour.</p>
              <ul>{string.Join(string.Empty, listItems)}</ul>
              <p>If you did not ask for this, you can ignore this email. Nothing will change.</p>
              <p>{name}</p>
            </div>
            """;
    }
}
