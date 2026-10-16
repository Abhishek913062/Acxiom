using AcxiomCRM.Models;

namespace AcxiomCRM.ViewModels;

public sealed class DashboardViewModel
{
    public int CustomerCount { get; init; }
    public int OpenLeadCount { get; init; }
    public int DueActivityCount { get; init; }
    public decimal OpenPipelineValue { get; init; }
    public decimal WeightedPipelineValue { get; init; }
    public decimal WonThisMonthValue { get; init; }
    public IReadOnlyList<PipelineStageSummary> Pipeline { get; init; } = Array.Empty<PipelineStageSummary>();
    public IReadOnlyList<UpcomingActivityItem> UpcomingActivities { get; init; } = Array.Empty<UpcomingActivityItem>();
    public IReadOnlyList<RecentLeadItem> RecentLeads { get; init; } = Array.Empty<RecentLeadItem>();
}

public sealed class PipelineStageSummary
{
    public OpportunityStage Stage { get; init; }
    public int Count { get; init; }
    public decimal Amount { get; init; }
}

public sealed class UpcomingActivityItem
{
    public int Id { get; init; }
    public string Subject { get; init; } = string.Empty;
    public ActivityType Type { get; init; }
    public ActivityPriority Priority { get; init; }
    public DateTime? DueAt { get; init; }
    public string RelatedTo { get; init; } = string.Empty;
}

public sealed class RecentLeadItem
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Company { get; init; }
    public LeadStatus Status { get; init; }
    public int Score { get; init; }
    public DateTime CreatedAt { get; init; }
}
