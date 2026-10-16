using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace AcxiomCRM.Models;

public class Opportunity : AuditableEntity, IValidatableObject
{
    [Required, StringLength(200)]
    public string Name { get; set; } = string.Empty;

    [DataType(DataType.MultilineText)]
    [StringLength(4000)]
    public string? Description { get; set; }

    /// <summary>An opportunity can be attached to a customer, a lead, or both after conversion.</summary>
    public int? CustomerId { get; set; }

    public Customer? Customer { get; set; }

    public int? LeadId { get; set; }

    public Lead? Lead { get; set; }

    public OpportunityStage Stage { get; set; } = OpportunityStage.Prospecting;

    [Range(typeof(decimal), "0.01", "999999999999.99")]
    [DataType(DataType.Currency)]
    public decimal Amount { get; set; }

    [Range(0, 100)]
    [Display(Name = "Win probability (%)")]
    public int Probability { get; set; }

    [Required, DataType(DataType.Date)]
    [Display(Name = "Expected close date")]
    public DateTime ExpectedCloseDate { get; set; }

    [DataType(DataType.Date)]
    [Display(Name = "Actual close date")]
    public DateTime? ActualCloseDate { get; set; }

    [Required, StringLength(450)]
    [Display(Name = "Assigned to")]
    public string AssignedToUserId { get; set; } = string.Empty;

    public ApplicationUser? AssignedToUser { get; set; }

    public ICollection<Activity> Activities { get; set; } = new List<Activity>();

    public bool IsClosed => Stage is OpportunityStage.ClosedWon or OpportunityStage.ClosedLost;

    public void UpdateStage(OpportunityStage stage, DateTime? closedAtUtc = null)
    {
        Stage = stage;

        if (stage == OpportunityStage.ClosedWon)
        {
            Probability = 100;
            ActualCloseDate = closedAtUtc ?? DateTime.UtcNow;
        }
        else if (stage == OpportunityStage.ClosedLost)
        {
            Probability = 0;
            ActualCloseDate = closedAtUtc ?? DateTime.UtcNow;
        }
        else
        {
            ActualCloseDate = null;
        }
    }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (CustomerId is null && LeadId is null)
        {
            yield return new ValidationResult(
                "An opportunity must be linked to a customer or lead.",
                new[] { nameof(CustomerId), nameof(LeadId) });
        }

        if (Stage == OpportunityStage.ClosedWon && Probability != 100)
        {
            yield return new ValidationResult(
                "A won opportunity must have a 100% probability.",
                new[] { nameof(Stage), nameof(Probability) });
        }

        if (Stage == OpportunityStage.ClosedLost && Probability != 0)
        {
            yield return new ValidationResult(
                "A lost opportunity must have a 0% probability.",
                new[] { nameof(Stage), nameof(Probability) });
        }

        if (IsClosed && ActualCloseDate is null)
        {
            yield return new ValidationResult(
                "A closed opportunity must have an actual close date.",
                new[] { nameof(ActualCloseDate), nameof(Stage) });
        }

        if (!IsClosed && ActualCloseDate is not null)
        {
            yield return new ValidationResult(
                "Only closed opportunities may have an actual close date.",
                new[] { nameof(ActualCloseDate), nameof(Stage) });
        }
    }
}
