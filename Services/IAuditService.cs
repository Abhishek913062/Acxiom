using System;
using System.Threading;
using System.Threading.Tasks;
using AcxiomCRM.Models;

namespace AcxiomCRM.Services;

public interface IAuditService
{
    /// <summary>
    /// Writes an append-only audit entry. Callers should pass DTO-like values only;
    /// the implementation additionally redacts common credential property names.
    /// </summary>
    Task LogAsync(
        AuditAction action,
        string entityName,
        string? entityId = null,
        string? details = null,
        object? oldValues = null,
        object? newValues = null,
        CancellationToken cancellationToken = default);
}
