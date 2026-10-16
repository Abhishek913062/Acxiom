using System.Collections.Generic;

namespace AcxiomCRM.Models;

public enum CustomerStatus
{
    Prospect = 0,
    Active = 1,
    Inactive = 2
}

public enum LeadStatus
{
    New = 0,
    Contacted = 1,
    Qualified = 2,
    Nurturing = 3,
    Converted = 4,
    Lost = 5
}

public enum LeadSource
{
    Website = 0,
    Referral = 1,
    SocialMedia = 2,
    Campaign = 3,
    ColdCall = 4,
    Event = 5,
    Partner = 6,
    Other = 7
}

public enum OpportunityStage
{
    Prospecting = 0,
    Qualification = 1,
    NeedsAnalysis = 2,
    Proposal = 3,
    Negotiation = 4,
    ClosedWon = 5,
    ClosedLost = 6
}

public enum ActivityType
{
    Call = 0,
    Email = 1,
    Meeting = 2,
    Task = 3,
    Note = 4
}

public enum ActivityStatus
{
    Pending = 0,
    Completed = 1,
    Cancelled = 2
}

public enum ActivityPriority
{
    Low = 0,
    Medium = 1,
    High = 2,
    Urgent = 3
}

public enum AuditAction
{
    Created = 0,
    Updated = 1,
    Deleted = 2,
    Viewed = 3,
    Converted = 4,
    StageChanged = 5,
    Completed = 6,
    Assigned = 7,
    Login = 8,
    Logout = 9,
    PasswordChanged = 10,
    RoleChanged = 11,
    AccountLocked = 12
}

/// <summary>
/// Identity role names used throughout the application. Keep these values stable,
/// because they are persisted in ASP.NET Core Identity tables.
/// </summary>
public static class ApplicationRoles
{
    public const string Admin = "Admin";
    public const string Manager = "Manager";
    public const string SalesExecutive = "Sales Executive";

    public static IReadOnlyList<string> All { get; } =
        new[] { Admin, Manager, SalesExecutive };
}
