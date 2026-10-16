using System;
using System.Linq;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using AcxiomCRM.Data;
using AcxiomCRM.Models;
using Microsoft.AspNetCore.Http;

namespace AcxiomCRM.Services;

/// <summary>
/// Persists important business and security actions without coupling controllers
/// to the AuditLogs table. It never stores common password/token fields verbatim.
/// </summary>
public class AuditService : IAuditService
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false
    };

    private readonly ApplicationDbContext _context;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public AuditService(ApplicationDbContext context, IHttpContextAccessor httpContextAccessor)
    {
        _context = context;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task LogAsync(
        AuditAction action,
        string entityName,
        string? entityId = null,
        string? details = null,
        object? oldValues = null,
        object? newValues = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(entityName))
        {
            throw new ArgumentException("An audit entity name is required.", nameof(entityName));
        }

        var httpContext = _httpContextAccessor.HttpContext;
        var userId = httpContext?.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        _context.AuditLogs.Add(new AuditLog
        {
            Action = action,
            EntityName = entityName.Trim(),
            EntityId = entityId,
            Details = details,
            OldValues = SerializeForAudit(oldValues),
            NewValues = SerializeForAudit(newValues),
            UserId = userId,
            IpAddress = httpContext?.Connection.RemoteIpAddress?.ToString(),
            Timestamp = DateTime.UtcNow
        });

        await _context.SaveChangesAsync(cancellationToken);
    }

    private static string? SerializeForAudit(object? value)
    {
        if (value is null)
        {
            return null;
        }

        try
        {
            var node = JsonSerializer.SerializeToNode(value, SerializerOptions);
            RedactSecrets(node);
            return node?.ToJsonString(SerializerOptions);
        }
        catch (NotSupportedException)
        {
            // Some callers may pass an object with unsupported members. A type name
            // preserves useful forensic context without risking an exception in the
            // primary business operation.
            return JsonSerializer.Serialize(new { valueType = value.GetType().FullName }, SerializerOptions);
        }
    }

    private static void RedactSecrets(JsonNode? node)
    {
        if (node is JsonObject jsonObject)
        {
            foreach (var property in jsonObject.ToList())
            {
                if (IsSensitiveName(property.Key))
                {
                    jsonObject[property.Key] = "[REDACTED]";
                }
                else
                {
                    RedactSecrets(property.Value);
                }
            }
        }
        else if (node is JsonArray jsonArray)
        {
            foreach (var child in jsonArray)
            {
                RedactSecrets(child);
            }
        }
    }

    private static bool IsSensitiveName(string name)
    {
        var normalized = name.Replace("_", string.Empty, StringComparison.Ordinal).ToUpperInvariant();
        return normalized.Contains("PASSWORD", StringComparison.Ordinal)
               || normalized.Contains("TOKEN", StringComparison.Ordinal)
               || normalized.Contains("SECRET", StringComparison.Ordinal)
               || normalized.Contains("SECURITYSTAMP", StringComparison.Ordinal)
               || normalized.Contains("CREDENTIAL", StringComparison.Ordinal)
               || normalized.Contains("ACCESSKEY", StringComparison.Ordinal);
    }
}
