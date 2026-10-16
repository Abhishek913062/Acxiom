using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;

namespace AcxiomCRM.Services;

/// <summary>
/// Provides role-aware ownership checks. Controllers can use the returned IDs in
/// EF queries to ensure that a Sales Executive sees only their records while a
/// Manager sees their reporting tree and an Admin sees all records.
/// </summary>
public interface IDataScopeService
{
    bool IsAdmin(ClaimsPrincipal user);

    bool IsManager(ClaimsPrincipal user);

    Task<IReadOnlyCollection<string>> GetAccessibleUserIdsAsync(
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default);

    Task<bool> CanAccessAsync(
        ClaimsPrincipal user,
        string? ownerOrAssigneeId,
        CancellationToken cancellationToken = default);

    Task<bool> CanManageUserAsync(
        ClaimsPrincipal user,
        string? targetUserId,
        CancellationToken cancellationToken = default);
}
