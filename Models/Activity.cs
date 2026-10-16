using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace AcxiomCRM.Models;

/// <summary>Represents a follow-up, contact attempt, meeting, note, or task.</summary>
public class Activity : AuditableEntity, IValidatableObject
{
    public ActivityType Type { get; set; } = ActivityType.Task;

    [Required, StringLength(200)]
    public string Subject { get; set; } = string.Empty;

    [DataType(DataType.MultilineText)]
    [StringLength(4000)]
    public string? Description { get; set; }

    public ActivityStatus Status { get; set; } = ActivityStatus.Pending;

    public ActivityPriority Priority { get; set; } = ActivityPriority.Medium;

    [DataType(DataType.DateTime)]
    [Display(Name = "Due date")]
    public DateTime? DueAt { get; set; }

    [DataType(DataType.DateTime)]
    [Display(Name = "Completed date")]
    public DateTime? CompletedAt { get; set; }

    [StringLength(2000)]
    public string? Outcome { get; set; }

    public int? CustomerId { get; set; }

    public Customer? Customer { get; set; }

    public int? LeadId { get; set; }

    public Lead? Lead { get; set; }

    public int? OpportunityId { get; set; }

    public Opportunity? Opportunity { get; set; }

    [Required, StringLength(450)]
    [Display(Name = "Assigned to")]
    public string AssignedToUserId { get; set; } = string.Empty;

    public ApplicationUser? AssignedToUser { get; set; }

    public bool IsOverdue => Status == ActivityStatus.Pending && DueAt is not null && DueAt < DateTime.UtcNow;

    public void MarkComplete(DateTime completedAtUtc, string? outcome = null)
    {
        Status = ActivityStatus.Completed;
        CompletedAt = completedAtUtc;
        Outcome = outcome;
    }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (CustomerId is null && LeadId is null && OpportunityId is null)
        {
            yield return new ValidationResult(
                "An activity must be linked to a customer, lead, or opportunity.",
                new[] { nameof(CustomerId), nameof(LeadId), nameof(OpportunityId) });
        }

        if (Status == ActivityStatus.Completed && CompletedAt is null)
        {
            yield return new ValidationResult(
                "Completed activities must include a completed date.",
                new[] { nameof(Status), nameof(CompletedAt) });
        }

        if (Status != ActivityStatus.Completed && CompletedAt is not null)
        {
            yield return new ValidationResult(
                "Only completed activities may include a completed date.",
                new[] { nameof(Status), nameof(CompletedAt) });
        }
    }
}
