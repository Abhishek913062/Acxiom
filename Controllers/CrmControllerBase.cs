using AcxiomCRM.Data;
using AcxiomCRM.Models;
using AcxiomCRM.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Controllers;

/// <summary>Shared identity and role-scope helpers for CRM controllers.</summary>
[Authorize]
public abstract class CrmControllerBase : Controller
{
    protected readonly ApplicationDbContext Db;
    protected readonly UserManager<ApplicationUser> UserManager;
    protected readonly IDataScopeService DataScope;

    protected CrmControllerBase(
        ApplicationDbContext db,
        UserManager<ApplicationUser> userManager,
        IDataScopeService dataScope)
    {
        Db = db;
        UserManager = userManager;
        DataScope = dataScope;
    }

    protected string CurrentUserId => UserManager.GetUserId(User)
        ?? throw new InvalidOperationException("The current user could not be identified.");

    /// <summary>
    /// Admins see all data. Managers see their own and direct reports' records.
    /// Sales Executives only see their own records.
    /// </summary>
    protected async Task<IReadOnlyCollection<string>?> GetVisibleUserIdsAsync()
    {
        if (User.IsInRole(ApplicationRoles.Admin))
        {
            return null;
        }

        return await DataScope.GetAccessibleUserIdsAsync(User);
    }

    protected async Task<bool> CanAccessAsync(string? assignedUserId)
    {
        if (string.IsNullOrWhiteSpace(assignedUserId))
        {
            return false;
        }

        return await DataScope.CanAccessAsync(User, assignedUserId);
    }

    protected async Task<bool> CanAssignToAsync(string? userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return false;
        }

        // Sales executives cannot move work to another user. A manager may assign
        // work only within their own direct team; an admin can assign broadly.
        return await DataScope.CanManageUserAsync(User, userId);
    }

    protected IActionResult NotFoundOrForbidden() => NotFound();
}
