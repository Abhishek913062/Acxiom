using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using AcxiomCRM.Data;
using AcxiomCRM.Models;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Services;

public class DataScopeService : IDataScopeService
{
    private readonly ApplicationDbContext _context;

    public DataScopeService(ApplicationDbContext context)
    {
        _context = context;
    }

    public bool IsAdmin(ClaimsPrincipal user) => user.IsInRole(ApplicationRoles.Admin);

    public bool IsManager(ClaimsPrincipal user) => user.IsInRole(ApplicationRoles.Manager);

    public async Task<IReadOnlyCollection<string>> GetAccessibleUserIdsAsync(
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default)
    {
        var currentUserId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrWhiteSpace(currentUserId))
        {
            return Array.Empty<string>();
        }

        if (IsAdmin(user))
        {
            return await _context.Users
                .AsNoTracking()
                .Select(applicationUser => applicationUser.Id)
                .ToListAsync(cancellationToken);
        }

        if (!IsManager(user))
        {
            return new[] { currentUserId };
        }

        // Managers are deliberately limited to their direct team. This keeps scope
        // predictable and prevents an indirect reporting line from granting access.
        var reportingLines = await _context.Users
            .AsNoTracking()
            .Select(applicationUser => new ReportingLine(applicationUser.Id, applicationUser.ManagerId))
            .ToListAsync(cancellationToken);

        var reportsByManager = reportingLines
            .Where(line => !string.IsNullOrWhiteSpace(line.ManagerId))
            .GroupBy(line => line.ManagerId!, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Select(line => line.UserId).ToArray(), StringComparer.Ordinal);

        var accessibleIds = new HashSet<string>(StringComparer.Ordinal) { currentUserId };
        if (reportsByManager.TryGetValue(currentUserId, out var directReports))
        {
            accessibleIds.UnionWith(directReports);
        }

        return accessibleIds.ToArray();
    }

    public async Task<bool> CanAccessAsync(
        ClaimsPrincipal user,
        string? ownerOrAssigneeId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(ownerOrAssigneeId))
        {
            return false;
        }

        var accessibleUserIds = await GetAccessibleUserIdsAsync(user, cancellationToken);
        return accessibleUserIds.Contains(ownerOrAssigneeId, StringComparer.Ordinal);
    }

    public Task<bool> CanManageUserAsync(
        ClaimsPrincipal user,
        string? targetUserId,
        CancellationToken cancellationToken = default) =>
        CanAccessAsync(user, targetUserId, cancellationToken);

    private sealed record ReportingLine(string UserId, string? ManagerId);
}
