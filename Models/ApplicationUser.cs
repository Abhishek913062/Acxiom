using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Microsoft.AspNetCore.Identity;

namespace AcxiomCRM.Models;

/// <summary>
/// Extends the Identity user with CRM profile and reporting-line information.
/// Authorization roles remain in the standard ASP.NET Core Identity role tables.
/// </summary>
public class ApplicationUser : IdentityUser
{
    [PersonalData]
    [Required, StringLength(100)]
    [Display(Name = "First name")]
    public string FirstName { get; set; } = string.Empty;

    [PersonalData]
    [Required, StringLength(100)]
    [Display(Name = "Last name")]
    public string LastName { get; set; } = string.Empty;

    [PersonalData]
    [StringLength(100)]
    [Display(Name = "Job title")]
    public string? JobTitle { get; set; }

    /// <summary>Optional reporting manager; used to scope a manager's team records.</summary>
    [StringLength(450)]
    public string? ManagerId { get; set; }

    public ApplicationUser? Manager { get; set; }

    public ICollection<ApplicationUser> DirectReports { get; set; } = new List<ApplicationUser>();

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? LastLoginAt { get; set; }

    public ICollection<Customer> OwnedCustomers { get; set; } = new List<Customer>();

    public ICollection<Lead> AssignedLeads { get; set; } = new List<Lead>();

    public ICollection<Opportunity> AssignedOpportunities { get; set; } = new List<Opportunity>();

    public ICollection<Activity> AssignedActivities { get; set; } = new List<Activity>();

    public ICollection<AuditLog> AuditLogs { get; set; } = new List<AuditLog>();

    [Display(Name = "Name")]
    public string DisplayName => string.Join(" ", new[] { FirstName, LastName }.Where(part => !string.IsNullOrWhiteSpace(part)));
}
