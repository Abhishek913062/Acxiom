using AcxiomCRM.Data;
using AcxiomCRM.Models;
using AcxiomCRM.Services;
using AcxiomCRM.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Controllers;

[Authorize(Roles = ApplicationRoles.Admin + "," + ApplicationRoles.Manager)]
public sealed class ReportsController : CrmControllerBase
{
    public ReportsController(ApplicationDbContext db, UserManager<ApplicationUser> users, IDataScopeService scope)
        : base(db, users, scope)
    {
    }

    public async Task<IActionResult> Index()
    {
        var ids = await GetVisibleUserIdsAsync();
        var customers = Db.Customers.AsNoTracking();
        var leads = Db.Leads.AsNoTracking();
        var opportunities = Db.Opportunities.AsNoTracking();
        var activities = Db.Activities.AsNoTracking();
        if (ids is not null)
        {
            customers = customers.Where(item => ids.Contains(item.OwnerId));
            leads = leads.Where(item => ids.Contains(item.AssignedToUserId));
            opportunities = opportunities.Where(item => ids.Contains(item.AssignedToUserId));
            activities = activities.Where(item => ids.Contains(item.AssignedToUserId));
        }
        var openStages = new[] { OpportunityStage.Prospecting, OpportunityStage.Qualification, OpportunityStage.NeedsAnalysis, OpportunityStage.Proposal, OpportunityStage.Negotiation };
        var model = new ReportsViewModel
        {
            TotalCustomers = await customers.CountAsync(),
            OpenLeads = await leads.CountAsync(item => item.Status != LeadStatus.Converted && item.Status != LeadStatus.Lost),
            OpenPipeline = await opportunities.Where(item => openStages.Contains(item.Stage)).SumAsync(item => (decimal?)item.Amount) ?? 0m,
            WonRevenue = await opportunities.Where(item => item.Stage == OpportunityStage.ClosedWon).SumAsync(item => (decimal?)item.Amount) ?? 0m,
            PendingActivities = await activities.CountAsync(item => item.Status == ActivityStatus.Pending),
            CustomersByStatus = await customers.GroupBy(item => item.Status).Select(group => new ReportSlice { Label = group.Key.ToString(), Count = group.Count() }).OrderBy(item => item.Label).ToListAsync(),
            LeadsByStatus = await leads.GroupBy(item => item.Status).Select(group => new ReportSlice { Label = group.Key.ToString(), Count = group.Count() }).OrderBy(item => item.Label).ToListAsync(),
            LeadsBySource = await leads.GroupBy(item => item.Source).Select(group => new ReportSlice { Label = group.Key.ToString(), Count = group.Count() }).OrderByDescending(item => item.Count).ToListAsync(),
            PipelineByStage = await opportunities.GroupBy(item => item.Stage).Select(group => new PipelineReportSlice { Label = group.Key.ToString(), Count = group.Count(), Amount = group.Sum(item => item.Amount), WeightedAmount = group.Sum(item => item.Amount * item.Probability / 100m) }).OrderBy(item => item.Label).ToListAsync(),
            ActivitiesByStatus = await activities.GroupBy(item => item.Status).Select(group => new ReportSlice { Label = group.Key.ToString(), Count = group.Count() }).OrderBy(item => item.Label).ToListAsync()
        };
        ViewData["Section"] = "Intelligence";
        ViewData["PageTitle"] = "Reports";
        return View(model);
    }
}
