using System;
using System.ComponentModel.DataAnnotations;

namespace AcxiomCRM.Models;

/// <summary>Append-only record of security-sensitive and business-significant events.</summary>
public class AuditLog
{
    public long Id { get; set; }

    public AuditAction Action { get; set; }

    [Required, StringLength(100)]
    public string EntityName { get; set; } = string.Empty;

    [StringLength(128)]
    public string? EntityId { get; set; }

    [StringLength(2000)]
    public string? Details { get; set; }

    /// <summary>JSON snapshot supplied by the caller; do not place secrets in it.</summary>
    public string? OldValues { get; set; }

    /// <summary>JSON snapshot supplied by the caller; do not place secrets in it.</summary>
    public string? NewValues { get; set; }

    [StringLength(450)]
    public string? UserId { get; set; }

    public ApplicationUser? User { get; set; }

    [StringLength(64)]
    public string? IpAddress { get; set; }

    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
