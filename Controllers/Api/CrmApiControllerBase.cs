using AcxiomCRM.Data;
using AcxiomCRM.Models;
using AcxiomCRM.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace AcxiomCRM.Controllers.Api;

[ApiController]
[Authorize]
public abstract class CrmApiControllerBase : ControllerBase
{
    protected readonly ApplicationDbContext Db;
    protected readonly IDataScopeService Scope;
    protected readonly UserManager<ApplicationUser> Users;
    protected readonly IAuditService Audit;
    protected CrmApiControllerBase(ApplicationDbContext db, IDataScopeService scope, UserManager<ApplicationUser> users, IAuditService audit) => (Db, Scope, Users, Audit) = (db, scope, users, audit);
    protected string CurrentUserId => Users.GetUserId(User) ?? throw new InvalidOperationException("Current user is missing.");
    protected Task<IReadOnlyCollection<string>> AccessibleIdsAsync() => Scope.GetAccessibleUserIdsAsync(User, HttpContext.RequestAborted);
    protected Task<bool> CanAccessAsync(string ownerId) => Scope.CanAccessAsync(User, ownerId, HttpContext.RequestAborted);
}
