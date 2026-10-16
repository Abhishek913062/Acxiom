using AcxiomCRM.Data;
using AcxiomCRM.Models;
using AcxiomCRM.Services;
using AcxiomCRM.ViewModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Controllers;

public sealed class ActivitiesController : CrmControllerBase
{
    private readonly IAuditService _audit;

    public ActivitiesController(ApplicationDbContext db, UserManager<ApplicationUser> users, IDataScopeService scope, IAuditService audit)
        : base(db, users, scope) => _audit = audit;

    public async Task<IActionResult> Index(ActivityStatus? status, ActivityType? type, string? search)
    {
        var ids = await GetVisibleUserIdsAsync();
        var query = Db.Activities.AsNoTracking().Include(item => item.Customer).Include(item => item.Lead).Include(item => item.Opportunity).Include(item => item.AssignedToUser).AsQueryable();
        if (ids is not null) query = query.Where(item => ids.Contains(item.AssignedToUserId));
        if (status is not null) query = query.Where(item => item.Status == status);
        if (type is not null) query = query.Where(item => item.Type == type);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(item => item.Subject.Contains(term) || (item.Description != null && item.Description.Contains(term)));
        }
        ViewData["Status"] = status;
        ViewData["Type"] = type;
        ViewData["Search"] = search;
        ViewData["Section"] = "Sales execution";
        ViewData["PageTitle"] = "Follow-ups & activities";
        return View(await query.OrderBy(item => item.Status == ActivityStatus.Completed).ThenBy(item => item.DueAt).ToListAsync());
    }

    public async Task<IActionResult> Details(int? id)
    {
        if (id is null) return NotFound();
        var activity = await Db.Activities.Include(item => item.Customer).Include(item => item.Lead).Include(item => item.Opportunity).Include(item => item.AssignedToUser).FirstOrDefaultAsync(item => item.Id == id.Value);
        if (activity is null || !await CanAccessAsync(activity.AssignedToUserId)) return NotFoundOrForbidden();
        ViewData["Section"] = "Sales execution";
        ViewData["PageTitle"] = activity.Subject;
        return View(activity);
    }

    public async Task<IActionResult> Create(int? customerId, int? leadId, int? opportunityId)
    {
        var model = new Activity { AssignedToUserId = CurrentUserId, CustomerId = customerId, LeadId = leadId, OpportunityId = opportunityId, DueAt = DateTime.UtcNow.AddDays(1) };
        await PopulateOptionsAsync(model);
        ViewData["Section"] = "Sales execution";
        ViewData["PageTitle"] = "New follow-up";
        return View(model);
    }

    [HttpPost]
    public async Task<IActionResult> Create([Bind("Type,Subject,Description,Status,Priority,DueAt,CompletedAt,Outcome,CustomerId,LeadId,OpportunityId,AssignedToUserId")] Activity activity)
    {
        ApplyStatusRules(activity);
        Revalidate(activity);
        await ValidateAccessAsync(activity);
        if (!ModelState.IsValid)
        {
            await PopulateOptionsAsync(activity);
            return View(activity);
        }
        Db.Activities.Add(activity);
        await Db.SaveChangesAsync();
        await _audit.LogAsync(AuditAction.Created, "Activity", activity.Id.ToString(), $"Created {activity.Type} activity '{activity.Subject}'.", newValues: new { activity.Type, activity.Status, activity.DueAt, activity.AssignedToUserId });
        TempData["SuccessMessage"] = "Follow-up created successfully.";
        return RedirectToAction(nameof(Details), new { id = activity.Id });
    }

    public async Task<IActionResult> Edit(int? id)
    {
        if (id is null) return NotFound();
        var activity = await Db.Activities.FindAsync(id.Value);
        if (activity is null || !await CanAccessAsync(activity.AssignedToUserId)) return NotFoundOrForbidden();
        await PopulateOptionsAsync(activity);
        ViewData["Section"] = "Sales execution";
        ViewData["PageTitle"] = "Edit activity";
        return View(activity);
    }

    [HttpPost]
    public async Task<IActionResult> Edit(int id, [Bind("Id,Type,Subject,Description,Status,Priority,DueAt,CompletedAt,Outcome,CustomerId,LeadId,OpportunityId,AssignedToUserId,RowVersion")] Activity input)
    {
        if (id != input.Id) return NotFound();
        var activity = await Db.Activities.FirstOrDefaultAsync(item => item.Id == id);
        if (activity is null || !await CanAccessAsync(activity.AssignedToUserId)) return NotFoundOrForbidden();
        ApplyStatusRules(input);
        Revalidate(input);
        await ValidateAccessAsync(input);
        if (!ModelState.IsValid)
        {
            await PopulateOptionsAsync(input);
            return View(input);
        }
        var oldValues = new { activity.Status, activity.DueAt, activity.AssignedToUserId, activity.Priority };
        Db.Entry(activity).Property(item => item.RowVersion).OriginalValue = input.RowVersion;
        activity.Type = input.Type;
        activity.Subject = input.Subject;
        activity.Description = input.Description;
        activity.Status = input.Status;
        activity.Priority = input.Priority;
        activity.DueAt = input.DueAt;
        activity.CompletedAt = input.CompletedAt;
        activity.Outcome = input.Outcome;
        activity.CustomerId = input.CustomerId;
        activity.LeadId = input.LeadId;
        activity.OpportunityId = input.OpportunityId;
        activity.AssignedToUserId = input.AssignedToUserId;
        try { await Db.SaveChangesAsync(); }
        catch (DbUpdateConcurrencyException)
        {
            ModelState.AddModelError(string.Empty, "This activity changed while you were editing it. Reload and try again.");
            await PopulateOptionsAsync(input);
            return View(input);
        }
        await _audit.LogAsync(oldValues.Status == activity.Status ? AuditAction.Updated : activity.Status == ActivityStatus.Completed ? AuditAction.Completed : AuditAction.Updated, "Activity", activity.Id.ToString(), $"Updated activity '{activity.Subject}'.", oldValues, new { activity.Status, activity.DueAt, activity.AssignedToUserId, activity.Priority });
        TempData["SuccessMessage"] = "Activity updated successfully.";
        return RedirectToAction(nameof(Details), new { id = activity.Id });
    }

    [HttpPost]
    public async Task<IActionResult> Complete(CompleteActivityViewModel input)
    {
        if (!ModelState.IsValid) return RedirectToAction(nameof(Details), new { id = input.Id });
        var activity = await Db.Activities.FindAsync(input.Id);
        if (activity is null || !await CanAccessAsync(activity.AssignedToUserId)) return NotFoundOrForbidden();
        if (activity.Status != ActivityStatus.Completed)
        {
            activity.MarkComplete(DateTime.UtcNow, input.Outcome);
            await Db.SaveChangesAsync();
            await _audit.LogAsync(AuditAction.Completed, "Activity", activity.Id.ToString(), $"Completed activity '{activity.Subject}'.", newValues: new { activity.CompletedAt, activity.Outcome });
            TempData["SuccessMessage"] = "Activity marked complete.";
        }
        return RedirectToAction(nameof(Details), new { id = activity.Id });
    }

    public async Task<IActionResult> Delete(int? id)
    {
        if (id is null) return NotFound();
        var activity = await Db.Activities.Include(item => item.AssignedToUser).FirstOrDefaultAsync(item => item.Id == id.Value);
        if (activity is null || !await CanAccessAsync(activity.AssignedToUserId)) return NotFoundOrForbidden();
        return View(activity);
    }

    [HttpPost, ActionName("Delete")]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var activity = await Db.Activities.FindAsync(id);
        if (activity is null || !await CanAccessAsync(activity.AssignedToUserId)) return NotFoundOrForbidden();
        var subject = activity.Subject;
        Db.Activities.Remove(activity);
        await Db.SaveChangesAsync();
        await _audit.LogAsync(AuditAction.Deleted, "Activity", id.ToString(), $"Deleted activity '{subject}'.");
        TempData["SuccessMessage"] = "Activity deleted.";
        return RedirectToAction(nameof(Index));
    }

    private static void ApplyStatusRules(Activity activity)
    {
        if (activity.Status == ActivityStatus.Completed) activity.CompletedAt ??= DateTime.UtcNow;
        else activity.CompletedAt = null;
    }

    private void Revalidate(Activity activity)
    {
        ModelState.Clear();
        TryValidateModel(activity);
    }

    private async Task ValidateAccessAsync(Activity activity)
    {
        if (!await CanAssignToAsync(activity.AssignedToUserId)) ModelState.AddModelError(nameof(activity.AssignedToUserId), "You can only assign an activity within your accessible team.");
        if (activity.CustomerId is not null)
        {
            var customer = await Db.Customers.AsNoTracking().FirstOrDefaultAsync(item => item.Id == activity.CustomerId);
            if (customer is null || !await CanAccessAsync(customer.OwnerId)) ModelState.AddModelError(nameof(activity.CustomerId), "Choose a customer you can access.");
        }
        if (activity.LeadId is not null)
        {
            var lead = await Db.Leads.AsNoTracking().FirstOrDefaultAsync(item => item.Id == activity.LeadId);
            if (lead is null || !await CanAccessAsync(lead.AssignedToUserId)) ModelState.AddModelError(nameof(activity.LeadId), "Choose a lead you can access.");
        }
        if (activity.OpportunityId is not null)
        {
            var opportunity = await Db.Opportunities.AsNoTracking().FirstOrDefaultAsync(item => item.Id == activity.OpportunityId);
            if (opportunity is null || !await CanAccessAsync(opportunity.AssignedToUserId)) ModelState.AddModelError(nameof(activity.OpportunityId), "Choose an opportunity you can access.");
        }
    }

    private async Task PopulateOptionsAsync(Activity model)
    {
        var ids = await GetVisibleUserIdsAsync();
        var customers = Db.Customers.AsNoTracking();
        var leads = Db.Leads.AsNoTracking();
        var opportunities = Db.Opportunities.AsNoTracking();
        var users = Db.Users.AsNoTracking().Where(user => user.IsActive);
        if (ids is not null)
        {
            customers = customers.Where(item => ids.Contains(item.OwnerId));
            leads = leads.Where(item => ids.Contains(item.AssignedToUserId));
            opportunities = opportunities.Where(item => ids.Contains(item.AssignedToUserId));
            users = users.Where(item => ids.Contains(item.Id));
        }
        ViewData["Customers"] = new SelectList(await customers.OrderBy(item => item.CompanyName).ThenBy(item => item.LastName).Select(item => new { item.Id, Name = item.CompanyName ?? (item.FirstName + " " + item.LastName) }).ToListAsync(), "Id", "Name", model.CustomerId);
        ViewData["Leads"] = new SelectList(await leads.OrderBy(item => item.CompanyName).ThenBy(item => item.LastName).Select(item => new { item.Id, Name = item.CompanyName ?? (item.FirstName + " " + item.LastName) }).ToListAsync(), "Id", "Name", model.LeadId);
        ViewData["Opportunities"] = new SelectList(await opportunities.OrderBy(item => item.Name).Select(item => new { item.Id, item.Name }).ToListAsync(), "Id", "Name", model.OpportunityId);
        ViewData["AssignableUsers"] = new SelectList(await users.OrderBy(item => item.FirstName).ThenBy(item => item.LastName).Select(item => new { item.Id, Name = item.FirstName + " " + item.LastName }).ToListAsync(), "Id", "Name", model.AssignedToUserId);
    }
}
