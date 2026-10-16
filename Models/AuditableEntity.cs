using System;
using System.ComponentModel.DataAnnotations;

namespace AcxiomCRM.Models;

/// <summary>
/// Shared persistence fields for mutable CRM records. The context owns timestamps
/// and EF Core uses RowVersion for optimistic concurrency protection.
/// </summary>
public abstract class AuditableEntity
{
    public int Id { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    [Timestamp]
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}
