using System.ComponentModel.DataAnnotations;
using AcxiomCRM.Models;

namespace AcxiomCRM.ViewModels;

public sealed class CustomerUpsertDto
{
    [Required, StringLength(100)] public string FirstName { get; set; } = string.Empty;
    [Required, StringLength(100)] public string LastName { get; set; } = string.Empty;
    [StringLength(200)] public string? CompanyName { get; set; }
    [Required, EmailAddress, StringLength(256)] public string Email { get; set; } = string.Empty;
    [Phone, StringLength(30)] public string? Phone { get; set; }
    public CustomerStatus Status { get; set; } = CustomerStatus.Prospect;
    [StringLength(450)] public string? OwnerId { get; set; }
    [StringLength(2000)] public string? Notes { get; set; }
}

public sealed record CustomerDto(int Id, string FirstName, string LastName, string? CompanyName, string Email, string? Phone, CustomerStatus Status, string OwnerId, DateTime CreatedAt);

public sealed class LeadUpsertDto
{
    [Required, StringLength(100)] public string FirstName { get; set; } = string.Empty;
    [Required, StringLength(100)] public string LastName { get; set; } = string.Empty;
    [StringLength(200)] public string? CompanyName { get; set; }
    [EmailAddress, StringLength(256)] public string? Email { get; set; }
    [Phone, StringLength(30)] public string? Phone { get; set; }
    [StringLength(100)] public string? JobTitle { get; set; }
    public LeadSource Source { get; set; } = LeadSource.Website;
    public LeadStatus Status { get; set; } = LeadStatus.New;
    [Range(0, 100)] public int Score { get; set; }
    [Range(typeof(decimal), "0", "999999999999.99")] public decimal? EstimatedValue { get; set; }
    public DateTime? NextFollowUpAt { get; set; }
    [StringLength(450)] public string? AssignedToUserId { get; set; }
    [StringLength(4000)] public string? Notes { get; set; }
}

public sealed record LeadDto(int Id, string FirstName, string LastName, string? CompanyName, string? Email, LeadSource Source, LeadStatus Status, int Score, decimal? EstimatedValue, string AssignedToUserId, DateTime CreatedAt);

public sealed class OpportunityUpsertDto
{
    [Required, StringLength(200)] public string Name { get; set; } = string.Empty;
    [StringLength(4000)] public string? Description { get; set; }
    public int? CustomerId { get; set; }
    public int? LeadId { get; set; }
    public OpportunityStage Stage { get; set; } = OpportunityStage.Prospecting;
    [Range(typeof(decimal), "0.01", "999999999999.99")] public decimal Amount { get; set; }
    [Range(0, 100)] public int Probability { get; set; }
    [Required] public DateTime? ExpectedCloseDate { get; set; }
    [StringLength(450)] public string? AssignedToUserId { get; set; }
}

public sealed record OpportunityDto(int Id, string Name, int? CustomerId, int? LeadId, OpportunityStage Stage, decimal Amount, int Probability, DateTime ExpectedCloseDate, DateTime? ActualCloseDate, string AssignedToUserId, DateTime CreatedAt);
