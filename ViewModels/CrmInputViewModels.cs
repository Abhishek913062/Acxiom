using System.ComponentModel.DataAnnotations;
using AcxiomCRM.Models;

namespace AcxiomCRM.ViewModels;

public sealed class LeadConversionViewModel
{
    [Required]
    public int LeadId { get; set; }
    public string LeadName { get; set; } = string.Empty;
    [Required, StringLength(100), Display(Name = "First name")]
    public string FirstName { get; set; } = string.Empty;
    [Required, StringLength(100), Display(Name = "Last name")]
    public string LastName { get; set; } = string.Empty;
    [StringLength(200), Display(Name = "Company")]
    public string? CompanyName { get; set; }
    [Required, EmailAddress, StringLength(256)]
    public string Email { get; set; } = string.Empty;
    [Phone, StringLength(30)]
    public string? Phone { get; set; }
    [Required, StringLength(450), Display(Name = "Customer owner")]
    public string OwnerId { get; set; } = string.Empty;
    public CustomerStatus Status { get; set; } = CustomerStatus.Active;
    [StringLength(2000)]
    public string? Notes { get; set; }
}

public sealed class CompleteActivityViewModel
{
    [Required]
    public int Id { get; set; }
    [StringLength(2000)]
    public string? Outcome { get; set; }
}

public sealed class UserAdminViewModel
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? JobTitle { get; set; }
    public string? ManagerId { get; set; }
    public string? ManagerName { get; set; }
    public bool IsActive { get; set; }
    public bool IsLockedOut { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public IReadOnlyList<string> Roles { get; set; } = Array.Empty<string>();
}

public sealed class UserEditViewModel
{
    [Required]
    public string Id { get; set; } = string.Empty;
    [Required, StringLength(100), Display(Name = "First name")]
    public string FirstName { get; set; } = string.Empty;
    [Required, StringLength(100), Display(Name = "Last name")]
    public string LastName { get; set; } = string.Empty;
    [StringLength(100), Display(Name = "Job title")]
    public string? JobTitle { get; set; }
    [Display(Name = "Manager")]
    public string? ManagerId { get; set; }
    [Display(Name = "Account active")]
    public bool IsActive { get; set; }
    [Display(Name = "Role")]
    public string Role { get; set; } = ApplicationRoles.SalesExecutive;
}

public sealed class RoleNameViewModel
{
    [Required, StringLength(100), RegularExpression("^[A-Za-z][A-Za-z0-9 ]*$", ErrorMessage = "Use letters, numbers, and spaces only.")]
    [Display(Name = "Role name")]
    public string Name { get; set; } = string.Empty;
}

public sealed class RoleSummaryViewModel
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public int Members { get; init; }
    public bool IsCoreRole { get; init; }
}

public sealed class ReportsViewModel
{
    public int TotalCustomers { get; init; }
    public int OpenLeads { get; init; }
    public decimal OpenPipeline { get; init; }
    public decimal WonRevenue { get; init; }
    public int PendingActivities { get; init; }
    public IReadOnlyList<ReportSlice> CustomersByStatus { get; init; } = Array.Empty<ReportSlice>();
    public IReadOnlyList<ReportSlice> LeadsByStatus { get; init; } = Array.Empty<ReportSlice>();
    public IReadOnlyList<ReportSlice> LeadsBySource { get; init; } = Array.Empty<ReportSlice>();
    public IReadOnlyList<PipelineReportSlice> PipelineByStage { get; init; } = Array.Empty<PipelineReportSlice>();
    public IReadOnlyList<ReportSlice> ActivitiesByStatus { get; init; } = Array.Empty<ReportSlice>();
}

public sealed class ReportSlice
{
    public string Label { get; init; } = string.Empty;
    public int Count { get; init; }
}

public sealed class PipelineReportSlice
{
    public string Label { get; init; } = string.Empty;
    public int Count { get; init; }
    public decimal Amount { get; init; }
    public decimal WeightedAmount { get; init; }
}
