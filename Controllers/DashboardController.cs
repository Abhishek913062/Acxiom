using AcxiomCRM.Data;
using AcxiomCRM.Models;
using AcxiomCRM.Services;
using AcxiomCRM.ViewModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Controllers;

public sealed class DashboardController : CrmControllerBase
{
    public DashboardController(
        ApplicationDbContext db,
        UserManager<ApplicationUser> userManager,
        IDataScopeService dataScope)
        : base(db, userManager, dataScope)
    {
    }

    public async Task<IActionResult> Index()
    {
        var visibleUserIds = await GetVisibleUserIdsAsync();
        var customerQuery = Db.Customers.AsNoTracking();
        var leadQuery = Db.Leads.AsNoTracking();
        var opportunityQuery = Db.Opportunities.AsNoTracking();
        var activityQuery = Db.Activities.AsNoTracking();

        if (visibleUserIds is not null)
        {
            customerQuery = customerQuery.Where(customer => visibleUserIds.Contains(customer.OwnerId));
            leadQuery = leadQuery.Where(lead => visibleUserIds.Contains(lead.AssignedToUserId));
            opportunityQuery = opportunityQuery.Where(opportunity => visibleUserIds.Contains(opportunity.AssignedToUserId));
            activityQuery = activityQuery.Where(activity => visibleUserIds.Contains(activity.AssignedToUserId));
        }

        var now = DateTime.UtcNow;
        var todayEnd = now.Date.AddDays(1);
        var monthStart = new DateTime(now.Year, now.Month, 1);
        var openStages = new[]
        {
            OpportunityStage.Prospecting, OpportunityStage.Qualification,
            OpportunityStage.NeedsAnalysis, OpportunityStage.Proposal,
            OpportunityStage.Negotiation
        };

        var pipeline = await opportunityQuery
            .GroupBy(opportunity => opportunity.Stage)
            .Select(group => new PipelineStageSummary
            {
                Stage = group.Key,
                Count = group.Count(),
                Amount = group.Sum(opportunity => opportunity.Amount)
            })
            .OrderBy(item => item.Stage)
            .ToListAsync();

        var model = new DashboardViewModel
        {
            CustomerCount = await customerQuery.CountAsync(customer => customer.Status == CustomerStatus.Active),
            OpenLeadCount = await leadQuery.CountAsync(lead => lead.Status != LeadStatus.Converted && lead.Status != LeadStatus.Lost),
            DueActivityCount = await activityQuery.CountAsync(activity =>
                activity.Status == ActivityStatus.Pending && activity.DueAt != null && activity.DueAt < todayEnd),
            OpenPipelineValue = await opportunityQuery
                .Where(opportunity => openStages.Contains(opportunity.Stage))
                .SumAsync(opportunity => (decimal?)opportunity.Amount) ?? 0m,
            WeightedPipelineValue = await opportunityQuery
                .Where(opportunity => openStages.Contains(opportunity.Stage))
                .SumAsync(opportunity => (decimal?)(opportunity.Amount * opportunity.Probability / 100m)) ?? 0m,
            WonThisMonthValue = await opportunityQuery
                .Where(opportunity => opportunity.Stage == OpportunityStage.ClosedWon && opportunity.ActualCloseDate >= monthStart)
                .SumAsync(opportunity => (decimal?)opportunity.Amount) ?? 0m,
            Pipeline = pipeline,
            UpcomingActivities = await activityQuery
                .Where(activity => activity.Status == ActivityStatus.Pending && activity.DueAt != null)
                .OrderBy(activity => activity.DueAt)
                .Take(6)
                .Select(activity => new UpcomingActivityItem
                {
                    Id = activity.Id,
                    Subject = activity.Subject,
                    Type = activity.Type,
                    Priority = activity.Priority,
                    DueAt = activity.DueAt,
                    RelatedTo = activity.Customer != null ? activity.Customer.CompanyName ?? (activity.Customer.FirstName + " " + activity.Customer.LastName) :
                        activity.Lead != null ? activity.Lead.CompanyName ?? (activity.Lead.FirstName + " " + activity.Lead.LastName) :
                        activity.Opportunity != null ? activity.Opportunity.Name : "General"
                })
                .ToListAsync(),
            RecentLeads = await leadQuery
                .OrderByDescending(lead => lead.CreatedAt)
                .Take(5)
                .Select(lead => new RecentLeadItem
                {
                    Id = lead.Id,
                    Name = lead.FirstName + " " + lead.LastName,
                    Company = lead.CompanyName,
                    Status = lead.Status,
                    Score = lead.Score,
                    CreatedAt = lead.CreatedAt
                })
                .ToListAsync()
        };

        ViewData["Section"] = "Overview";
        ViewData["PageTitle"] = "Sales dashboard";
        return View(model);
    }
}
