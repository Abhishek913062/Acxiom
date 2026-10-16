using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace AcxiomCRM.Models;

public class Customer : AuditableEntity, IValidatableObject
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

    [Required, EmailAddress, StringLength(256)]
    public string Email { get; set; } = string.Empty;

    /// <summary>Normalized by <see cref="Data.ApplicationDbContext"/> and indexed uniquely.</summary>
    [StringLength(256)]
    public string NormalizedEmail { get; set; } = string.Empty;

    [Phone, StringLength(30)]
    public string? Phone { get; set; }

    [StringLength(200)]
    public string? AddressLine1 { get; set; }

    [StringLength(200)]
    public string? AddressLine2 { get; set; }

    [StringLength(100)]
    public string? City { get; set; }

    [StringLength(100)]
    [Display(Name = "State / Province")]
    public string? State { get; set; }

    [StringLength(20)]
    [Display(Name = "Postal code")]
    public string? PostalCode { get; set; }

    [StringLength(100)]
    public string? Country { get; set; }

    [StringLength(2000)]
    public string? Notes { get; set; }

    public CustomerStatus Status { get; set; } = CustomerStatus.Active;

    [Required, StringLength(450)]
    public string OwnerId { get; set; } = string.Empty;

    public ApplicationUser? Owner { get; set; }

    /// <summary>The lead from which this customer was created, when applicable.</summary>
    public Lead? ConvertedFromLead { get; set; }

    public ICollection<Opportunity> Opportunities { get; set; } = new List<Opportunity>();

    public ICollection<Activity> Activities { get; set; } = new List<Activity>();

    [Display(Name = "Customer")]
    public string DisplayName => string.IsNullOrWhiteSpace(CompanyName)
        ? $"{FirstName} {LastName}".Trim()
        : CompanyName;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (string.IsNullOrWhiteSpace(FirstName) && string.IsNullOrWhiteSpace(LastName))
        {
            yield return new ValidationResult(
                "A first name or last name is required.",
                new[] { nameof(FirstName), nameof(LastName) });
        }
    }
}
