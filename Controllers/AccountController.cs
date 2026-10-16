using AcxiomCRM.Models;
using AcxiomCRM.Services;
using AcxiomCRM.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace AcxiomCRM.Controllers;

/// <summary>
/// Handles the public account flow and the authenticated user's password rotation.
/// Identity owns password hashing, failed-attempt counting, and lockout enforcement.
/// </summary>
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class AccountController : Controller
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly IAuditService _auditService;
    private readonly ILogger<AccountController> _logger;

    public AccountController(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        RoleManager<IdentityRole> roleManager,
        IAuditService auditService,
        ILogger<AccountController> logger)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _roleManager = roleManager;
        _auditService = auditService;
        _logger = logger;
    }

    [AllowAnonymous]
    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToLocal(returnUrl);
        }

        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToLocal(model.ReturnUrl);
        }

        model.Email = model.Email?.Trim() ?? string.Empty;

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var user = await _userManager.FindByEmailAsync(model.Email);
        if (user is null || !user.IsActive)
        {
            // Keep this intentionally non-specific so the form does not reveal account state.
            ModelState.AddModelError(string.Empty, "Invalid email or password.");
            return View(model);
        }

        // lockoutOnFailure is deliberately true: Identity updates failed-attempt counters
        // and applies the configured account lockout policy for this user.
        var result = await _signInManager.PasswordSignInAsync(
            user,
            model.Password,
            model.RememberMe,
            lockoutOnFailure: true);

        if (result.Succeeded)
        {
            user.LastLoginAt = DateTime.UtcNow;
            var updateResult = await _userManager.UpdateAsync(user);
            if (!updateResult.Succeeded)
            {
                _logger.LogWarning("Could not record the successful login time for user {UserId}.", user.Id);
            }

            await WriteAuditAsync(AuditAction.Login, user.Id, "Successful sign-in.");
            return RedirectToLocal(model.ReturnUrl);
        }

        if (result.IsLockedOut)
        {
            await WriteAuditAsync(AuditAction.AccountLocked, user.Id, "Sign-in denied because the account is locked.");
            ModelState.AddModelError(string.Empty, "This account is temporarily locked after too many unsuccessful sign-in attempts. Please try again later.");
        }
        else if (result.RequiresTwoFactor)
        {
            // Two-factor authentication is not currently enabled in this application,
            // but avoid treating a future 2FA result as an ordinary authentication failure.
            ModelState.AddModelError(string.Empty, "Additional verification is required to sign in.");
        }
        else
        {
            ModelState.AddModelError(string.Empty, "Invalid email or password.");
        }

        return View(model);
    }

    [AllowAnonymous]
    [HttpGet]
    public IActionResult Register(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToLocal(returnUrl);
        }

        return View(new RegisterViewModel { ReturnUrl = returnUrl });
    }

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        model.Email = model.Email?.Trim() ?? string.Empty;
        model.FirstName = model.FirstName?.Trim() ?? string.Empty;
        model.LastName = model.LastName?.Trim() ?? string.Empty;

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        if (await _userManager.FindByEmailAsync(model.Email) is not null)
        {
            ModelState.AddModelError(nameof(model.Email), "An account with this email address already exists.");
            return View(model);
        }

        var roleResult = await EnsureSalesExecutiveRoleAsync();
        if (!roleResult.Succeeded)
        {
            AddIdentityErrors(roleResult);
            return View(model);
        }

        var user = new ApplicationUser
        {
            UserName = model.Email,
            Email = model.Email,
            FirstName = model.FirstName,
            LastName = model.LastName,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        var createResult = await _userManager.CreateAsync(user, model.Password);
        if (!createResult.Succeeded)
        {
            AddIdentityErrors(createResult);
            return View(model);
        }

        var addToRoleResult = await _userManager.AddToRoleAsync(user, ApplicationRoles.SalesExecutive);
        if (!addToRoleResult.Succeeded)
        {
            // Do not leave behind an account that has no application role.
            var deleteResult = await _userManager.DeleteAsync(user);
            if (!deleteResult.Succeeded)
            {
                _logger.LogError("User {UserId} could not be cleaned up after role assignment failed.", user.Id);
            }

            AddIdentityErrors(addToRoleResult);
            return View(model);
        }

        await _signInManager.SignInAsync(user, isPersistent: false);
        await WriteAuditAsync(AuditAction.Created, user.Id, "Account registered with Sales Executive access.");
        TempData["SuccessMessage"] = "Your AcxiomCRM account is ready. Welcome aboard!";

        return RedirectToLocal(model.ReturnUrl);
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is not null)
        {
            await WriteAuditAsync(AuditAction.Logout, user.Id, "Signed out.");
        }

        await _signInManager.SignOutAsync();
        TempData["SuccessMessage"] = "You have been signed out.";
        return RedirectToAction(nameof(Login));
    }

    [Authorize]
    [HttpGet]
    public IActionResult Manage() => RedirectToAction(nameof(ChangePassword));

    [Authorize]
    [HttpGet]
    public IActionResult ChangePassword() => View(new ChangePasswordViewModel());

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        if (string.Equals(model.CurrentPassword, model.NewPassword, StringComparison.Ordinal))
        {
            ModelState.AddModelError(nameof(model.NewPassword), "Choose a new password that is different from your current password.");
            return View(model);
        }

        var user = await _userManager.GetUserAsync(User);
        if (user is null)
        {
            await _signInManager.SignOutAsync();
            return Challenge();
        }

        var result = await _userManager.ChangePasswordAsync(user, model.CurrentPassword, model.NewPassword);
        if (!result.Succeeded)
        {
            AddIdentityErrors(result);
            return View(model);
        }

        // Refresh the authentication cookie after Identity rotates the security stamp.
        await _signInManager.RefreshSignInAsync(user);
        await WriteAuditAsync(AuditAction.PasswordChanged, user.Id, "Password changed.");
        TempData["SuccessMessage"] = "Your password has been updated securely.";
        return RedirectToAction("Index", "Dashboard");
    }

    [AllowAnonymous]
    [HttpGet]
    public IActionResult AccessDenied() => View();

    private async Task<IdentityResult> EnsureSalesExecutiveRoleAsync()
    {
        if (await _roleManager.RoleExistsAsync(ApplicationRoles.SalesExecutive))
        {
            return IdentityResult.Success;
        }

        var result = await _roleManager.CreateAsync(new IdentityRole(ApplicationRoles.SalesExecutive));
        if (!result.Succeeded && await _roleManager.RoleExistsAsync(ApplicationRoles.SalesExecutive))
        {
            // Another concurrent registration created the role first.
            return IdentityResult.Success;
        }

        return result;
    }

    private IActionResult RedirectToLocal(string? returnUrl) =>
        !string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl)
            ? Redirect(returnUrl)
            : RedirectToAction("Index", "Dashboard");

    private void AddIdentityErrors(IdentityResult result)
    {
        foreach (var error in result.Errors)
        {
            ModelState.AddModelError(string.Empty, error.Description);
        }
    }

    private async Task WriteAuditAsync(AuditAction action, string entityId, string details)
    {
        try
        {
            await _auditService.LogAsync(
                action,
                "User",
                entityId,
                details,
                cancellationToken: HttpContext.RequestAborted);
        }
        catch (Exception exception)
        {
            // Authentication must remain available if an auxiliary audit write fails.
            _logger.LogError(exception, "Could not write {Action} audit record for user {UserId}.", action, entityId);
        }
    }
}
