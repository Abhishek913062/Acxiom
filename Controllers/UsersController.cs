using AcxiomCRM.Data;
using AcxiomCRM.Models;
using AcxiomCRM.Services;
using AcxiomCRM.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Controllers;

[Authorize(Roles = ApplicationRoles.Admin)]
public sealed class UsersController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _users;
    private readonly IAuditService _audit;

    public UsersController(ApplicationDbContext db, UserManager<ApplicationUser> users, IAuditService audit)
    {
        _db = db;
        _users = users;
        _audit = audit;
    }

    public async Task<IActionResult> Index(string? search)
    {
        var query = _db.Users.AsNoTracking().Include(user => user.Manager).AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(user => user.FirstName.Contains(term) || user.LastName.Contains(term) || user.Email!.Contains(term));
        }
        var users = await query.OrderBy(user => user.FirstName).ThenBy(user => user.LastName).ToListAsync();
        var models = new List<UserAdminViewModel>();
        foreach (var user in users)
        {
            models.Add(new UserAdminViewModel
            {
                Id = user.Id,
                Name = user.DisplayName,
                Email = user.Email ?? string.Empty,
                JobTitle = user.JobTitle,
                ManagerId = user.ManagerId,
                ManagerName = user.Manager?.DisplayName,
                IsActive = user.IsActive,
                IsLockedOut = user.LockoutEnd is not null && user.LockoutEnd > DateTimeOffset.UtcNow,
                LastLoginAt = user.LastLoginAt,
                Roles = (await _users.GetRolesAsync(user)).ToArray()
            });
        }
        ViewData["Search"] = search;
        ViewData["Section"] = "Administration";
        ViewData["PageTitle"] = "Users";
        return View(models);
    }

    public async Task<IActionResult> Edit(string? id)
    {
        if (string.IsNullOrWhiteSpace(id)) return NotFound();
        var user = await _users.FindByIdAsync(id);
        if (user is null) return NotFound();
        await PopulateOptionsAsync(user.Id, user.ManagerId);
        var roles = await _users.GetRolesAsync(user);
        ViewData["Section"] = "Administration";
        ViewData["PageTitle"] = $"Edit {user.DisplayName}";
        return View(new UserEditViewModel
        {
            Id = user.Id,
            FirstName = user.FirstName,
            LastName = user.LastName,
            JobTitle = user.JobTitle,
            ManagerId = user.ManagerId,
            IsActive = user.IsActive,
            Role = roles.FirstOrDefault() ?? ApplicationRoles.SalesExecutive
        });
    }

    [HttpPost]
    public async Task<IActionResult> Edit(UserEditViewModel input)
    {
        var user = await _users.FindByIdAsync(input.Id);
        if (user is null) return NotFound();
        var originalRoles = await _users.GetRolesAsync(user);
        var currentUserId = _users.GetUserId(User);
        if (!ApplicationRoles.All.Contains(input.Role, StringComparer.Ordinal)) ModelState.AddModelError(nameof(input.Role), "Choose a valid application role.");
        if (input.ManagerId == user.Id) ModelState.AddModelError(nameof(input.ManagerId), "A user cannot manage themselves.");
        if (!string.IsNullOrWhiteSpace(input.ManagerId) && await _users.FindByIdAsync(input.ManagerId) is null) ModelState.AddModelError(nameof(input.ManagerId), "Choose a valid manager.");
        if (currentUserId == user.Id && !input.IsActive) ModelState.AddModelError(nameof(input.IsActive), "You cannot deactivate your own account.");
        if (currentUserId == user.Id && !string.Equals(input.Role, ApplicationRoles.Admin, StringComparison.Ordinal)) ModelState.AddModelError(nameof(input.Role), "You cannot remove your own Administrator access.");
        if (originalRoles.Contains(ApplicationRoles.Admin) && !string.Equals(input.Role, ApplicationRoles.Admin, StringComparison.Ordinal))
        {
            var admins = await _users.GetUsersInRoleAsync(ApplicationRoles.Admin);
            if (admins.Count <= 1) ModelState.AddModelError(nameof(input.Role), "At least one Administrator account must remain.");
        }
        if (!ModelState.IsValid)
        {
            await PopulateOptionsAsync(user.Id, input.ManagerId);
            return View(input);
        }

        var oldValues = new { user.FirstName, user.LastName, user.JobTitle, user.ManagerId, user.IsActive, Roles = originalRoles };
        user.FirstName = input.FirstName.Trim();
        user.LastName = input.LastName.Trim();
        user.JobTitle = input.JobTitle?.Trim();
        user.ManagerId = string.IsNullOrWhiteSpace(input.ManagerId) ? null : input.ManagerId;
        user.IsActive = input.IsActive;
        if (!input.IsActive) user.LockoutEnd = DateTimeOffset.MaxValue;
        else if (user.LockoutEnd == DateTimeOffset.MaxValue) user.LockoutEnd = null;
        var update = await _users.UpdateAsync(user);
        if (!update.Succeeded)
        {
            AddErrors(update);
            await PopulateOptionsAsync(user.Id, input.ManagerId);
            return View(input);
        }
        if (!originalRoles.Contains(input.Role, StringComparer.Ordinal))
        {
            var add = await _users.AddToRoleAsync(user, input.Role);
            if (!add.Succeeded)
            {
                AddErrors(add);
                await PopulateOptionsAsync(user.Id, input.ManagerId);
                return View(input);
            }
            var oldRoleNames = originalRoles.Where(role => !string.Equals(role, input.Role, StringComparison.Ordinal)).ToArray();
            if (oldRoleNames.Length > 0)
            {
                var remove = await _users.RemoveFromRolesAsync(user, oldRoleNames);
                if (!remove.Succeeded) AddErrors(remove);
            }
        }
        if (!input.IsActive) await _users.UpdateSecurityStampAsync(user);
        await _audit.LogAsync(AuditAction.RoleChanged, "User", user.Id, $"Updated user {user.Email}.", oldValues, new { user.FirstName, user.LastName, user.JobTitle, user.ManagerId, user.IsActive, Role = input.Role });
        TempData["SuccessMessage"] = "User settings updated.";
        return RedirectToAction(nameof(Index));
    }

    private async Task PopulateOptionsAsync(string userId, string? selectedManagerId = null)
    {
        var managers = await _db.Users.AsNoTracking().Where(user => user.IsActive && user.Id != userId)
            .OrderBy(user => user.FirstName).ThenBy(user => user.LastName)
            .Select(user => new { user.Id, Name = user.FirstName + " " + user.LastName + " (" + user.Email + ")" }).ToListAsync();
        ViewData["Managers"] = new SelectList(managers, "Id", "Name", selectedManagerId);
        ViewData["Roles"] = new SelectList(ApplicationRoles.All);
    }

    private void AddErrors(IdentityResult result)
    {
        foreach (var error in result.Errors) ModelState.AddModelError(string.Empty, error.Description);
    }
}
