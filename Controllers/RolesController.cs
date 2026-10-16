using AcxiomCRM.Models;
using AcxiomCRM.Services;
using AcxiomCRM.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Controllers;

[Authorize(Roles = ApplicationRoles.Admin)]
public sealed class RolesController : Controller
{
    private readonly RoleManager<IdentityRole> _roles;
    private readonly UserManager<ApplicationUser> _users;
    private readonly IAuditService _audit;
    public RolesController(RoleManager<IdentityRole> roles, UserManager<ApplicationUser> users, IAuditService audit) => (_roles, _users, _audit) = (roles, users, audit);

    public async Task<IActionResult> Index()
    {
        var roles = await _roles.Roles.OrderBy(role => role.Name).ToListAsync();
        var data = new List<RoleSummaryViewModel>();
        foreach (var role in roles)
        {
            data.Add(new RoleSummaryViewModel
            {
                Id = role.Id,
                Name = role.Name ?? string.Empty,
                Members = (await _users.GetUsersInRoleAsync(role.Name!)).Count,
                IsCoreRole = ApplicationRoles.All.Contains(role.Name, StringComparer.Ordinal)
            });
        }
        ViewData["Section"] = "Administration";
        ViewData["PageTitle"] = "Roles & access";
        return View(data);
    }

    [HttpPost]
    public async Task<IActionResult> Create(RoleNameViewModel input)
    {
        input.Name = input.Name?.Trim() ?? string.Empty;
        if (!ModelState.IsValid) return RedirectToAction(nameof(Index));
        if (await _roles.RoleExistsAsync(input.Name))
        {
            TempData["ErrorMessage"] = "That role already exists.";
            return RedirectToAction(nameof(Index));
        }
        var result = await _roles.CreateAsync(new IdentityRole(input.Name));
        if (!result.Succeeded)
        {
            TempData["ErrorMessage"] = string.Join(" ", result.Errors.Select(error => error.Description));
            return RedirectToAction(nameof(Index));
        }
        await _audit.LogAsync(AuditAction.Created, "Role", input.Name, $"Created role {input.Name}.");
        TempData["SuccessMessage"] = "Role created.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Delete(string? id)
    {
        if (string.IsNullOrWhiteSpace(id)) return NotFound();
        var role = await _roles.FindByIdAsync(id);
        if (role is null) return NotFound();
        if (ApplicationRoles.All.Contains(role.Name, StringComparer.Ordinal))
        {
            TempData["ErrorMessage"] = "Core application roles are protected.";
            return RedirectToAction(nameof(Index));
        }
        if ((await _users.GetUsersInRoleAsync(role.Name!)).Count > 0)
        {
            TempData["ErrorMessage"] = "Reassign members before deleting this role.";
            return RedirectToAction(nameof(Index));
        }
        var result = await _roles.DeleteAsync(role);
        TempData[result.Succeeded ? "SuccessMessage" : "ErrorMessage"] = result.Succeeded ? "Role deleted." : string.Join(" ", result.Errors.Select(error => error.Description));
        if (result.Succeeded) await _audit.LogAsync(AuditAction.Deleted, "Role", role.Id, $"Deleted role {role.Name}.");
        return RedirectToAction(nameof(Index));
    }
}
