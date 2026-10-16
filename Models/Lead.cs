using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace AcxiomCRM.Models;

public class Lead : AuditableEntity, IValidatableObject
{
    [Required, StringLength(100)]
    [Display(Name = "First name")]
    public string FirstName { get; set; } = string.Empty;

    [Required, StringLength(100)]
    [Display(Name = "Last name")]
    public string LastName { get; set; } = string.Empty;

    [StringLength(200)]
    [Display(Name = "Company")]
    public string? CompanyName { get; set; }

    [EmailAddress, StringLength(256)]
    public string? Email { get; set; }

    [StringLength(256)]
    public string? NormalizedEmail { get; set; }

    [Phone, StringLength(30)]
    public string? Phone { get; set; }

    [StringLength(100)]
    [Display(Name = "Job title")]
    public string? JobTitle { get; set; }

    public LeadSource Source { get; set; } = LeadSource.Website;

    public LeadStatus Status { get; set; } = LeadStatus.New;

    [Range(0, 100)]
    public int Score { get; set; }

    [Range(typeof(decimal), "0", "999999999999.99")]
    [Display(Name = "Estimated value")]
    public decimal? EstimatedValue { get; set; }

    [DataType(DataType.MultilineText)]
    [StringLength(4000)]
    public string? Notes { get; set; }

    [DataType(DataType.DateTime)]
    [Display(Name = "Next follow-up")]
    public DateTime? NextFollowUpAt { get; set; }

    [DataType(DataType.DateTime)]
    [Display(Name = "Last contacted")]
    public DateTime? LastContactedAt { get; set; }

    [Required, StringLength(450)]
    [Display(Name = "Assigned to")]
    public string AssignedToUserId { get; set; } = string.Empty;

    public ApplicationUser? AssignedToUser { get; set; }

    /// <summary>Set once the lead has been converted; one customer may originate from one lead.</summary>
    public int? ConvertedCustomerId { get; set; }

    public Customer? ConvertedCustomer { get; set; }

    public DateTime? ConvertedAt { get; set; }

    public ICollection<Opportunity> Opportunities { get; set; } = new List<Opportunity>();

    public ICollection<Activity> Activities { get; set; } = new List<Activity>();

    public bool IsConverted => Status == LeadStatus.Converted;

    public void MarkConverted(int customerId, DateTime convertedAtUtc)
    {
        if (customerId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(customerId));
        }

        if (Status == LeadStatus.Lost)
        {
            throw new InvalidOperationException("A lost lead cannot be converted.");
        }

        ConvertedCustomerId = customerId;
        ConvertedAt = convertedAtUtc;
        Status = LeadStatus.Converted;
    }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Status == LeadStatus.Converted && (ConvertedCustomerId is null || ConvertedAt is null))
        {
            yield return new ValidationResult(
                "A converted lead must reference its customer and conversion time.",
                new[] { nameof(ConvertedCustomerId), nameof(ConvertedAt), nameof(Status) });
        }

        if (Status != LeadStatus.Converted && (ConvertedCustomerId is not null || ConvertedAt is not null))
        {
            yield return new ValidationResult(
                "Only converted leads may contain conversion information.",
                new[] { nameof(ConvertedCustomerId), nameof(ConvertedAt), nameof(Status) });
        }
    }
}
