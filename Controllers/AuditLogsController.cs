using AcxiomCRM.Data;
using AcxiomCRM.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Controllers;

[Authorize(Roles = ApplicationRoles.Admin)]
public sealed class AuditLogsController : Controller
{
    private readonly ApplicationDbContext _db;
    public AuditLogsController(ApplicationDbContext db) => _db = db;
    public async Task<IActionResult> Index(string? entity, AuditAction? action)
    {
        var query = _db.AuditLogs.AsNoTracking().Include(log => log.User).AsQueryable();
        if (!string.IsNullOrWhiteSpace(entity)) query = query.Where(log => log.EntityName == entity.Trim());
        if (action is not null) query = query.Where(log => log.Action == action);
        ViewData["Entity"] = entity;
        ViewData["Action"] = action;
        ViewData["Entities"] = await _db.AuditLogs.AsNoTracking().Select(log => log.EntityName).Distinct().OrderBy(name => name).ToListAsync();
        ViewData["Section"] = "Administration";
        ViewData["PageTitle"] = "Audit log";
        return View(await query.OrderByDescending(log => log.Timestamp).Take(250).ToListAsync());
    }
}
