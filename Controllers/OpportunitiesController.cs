using AcxiomCRM.Data;
using AcxiomCRM.Models;
using AcxiomCRM.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Controllers;

public sealed class OpportunitiesController : CrmControllerBase
{
    private readonly IAuditService _audit;

    public OpportunitiesController(ApplicationDbContext db, UserManager<ApplicationUser> users, IDataScopeService scope, IAuditService audit)
        : base(db, users, scope) => _audit = audit;

    public async Task<IActionResult> Index(string? search, OpportunityStage? stage)
    {
        var ids = await GetVisibleUserIdsAsync();
        var query = Db.Opportunities.AsNoTracking().Include(item => item.Customer).Include(item => item.Lead).Include(item => item.AssignedToUser).AsQueryable();
        if (ids is not null) query = query.Where(item => ids.Contains(item.AssignedToUserId));
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(item => item.Name.Contains(term) || (item.Customer != null && item.Customer.CompanyName != null && item.Customer.CompanyName.Contains(term)) || (item.Lead != null && item.Lead.CompanyName != null && item.Lead.CompanyName.Contains(term)));
        }
        if (stage is not null) query = query.Where(item => item.Stage == stage);
        ViewData["Search"] = search;
        ViewData["Stage"] = stage;
        ViewData["Section"] = "Sales execution";
        ViewData["PageTitle"] = "Opportunities";
        return View(await query.OrderBy(item => item.ExpectedCloseDate).ToListAsync());
    }

    public async Task<IActionResult> Details(int? id)
    {
        if (id is null) return NotFound();
        var opportunity = await Db.Opportunities.Include(item => item.Customer).Include(item => item.Lead).Include(item => item.AssignedToUser)
            .Include(item => item.Activities.OrderByDescending(activity => activity.CreatedAt).Take(12)).FirstOrDefaultAsync(item => item.Id == id.Value);
        if (opportunity is null || !await CanAccessAsync(opportunity.AssignedToUserId)) return NotFoundOrForbidden();
        ViewData["Section"] = "Sales execution";
        ViewData["PageTitle"] = opportunity.Name;
        return View(opportunity);
    }

    public async Task<IActionResult> Create(int? customerId, int? leadId)
    {
        var input = new Opportunity { AssignedToUserId = CurrentUserId, CustomerId = customerId, LeadId = leadId, ExpectedCloseDate = DateTime.UtcNow.Date.AddDays(30), Probability = 10 };
        await PopulateOptionsAsync(input);
        ViewData["Section"] = "Sales execution";
        ViewData["PageTitle"] = "New opportunity";
        return View(input);
    }

    [HttpPost]
    public async Task<IActionResult> Create([Bind("Name,Description,CustomerId,LeadId,Stage,Amount,Probability,ExpectedCloseDate,ActualCloseDate,AssignedToUserId")] Opportunity opportunity)
    {
        ApplyStageRules(opportunity);
        Revalidate(opportunity);
        await ValidateAccessAsync(opportunity);
        if (!ModelState.IsValid)
        {
            await PopulateOptionsAsync(opportunity);
            return View(opportunity);
        }
        Db.Opportunities.Add(opportunity);
        await Db.SaveChangesAsync();
        await _audit.LogAsync(AuditAction.Created, "Opportunity", opportunity.Id.ToString(), $"Created opportunity {opportunity.Name}.", newValues: new { opportunity.Stage, opportunity.Amount, opportunity.Probability, opportunity.AssignedToUserId });
        TempData["SuccessMessage"] = "Opportunity created successfully.";
        return RedirectToAction(nameof(Details), new { id = opportunity.Id });
    }

    public async Task<IActionResult> Edit(int? id)
    {
        if (id is null) return NotFound();
        var opportunity = await Db.Opportunities.FindAsync(id.Value);
        if (opportunity is null || !await CanAccessAsync(opportunity.AssignedToUserId)) return NotFoundOrForbidden();
        await PopulateOptionsAsync(opportunity);
        ViewData["Section"] = "Sales execution";
        ViewData["PageTitle"] = "Edit opportunity";
        return View(opportunity);
    }

    [HttpPost]
    public async Task<IActionResult> Edit(int id, [Bind("Id,Name,Description,CustomerId,LeadId,Stage,Amount,Probability,ExpectedCloseDate,ActualCloseDate,AssignedToUserId,RowVersion")] Opportunity input)
    {
        if (id != input.Id) return NotFound();
        var opportunity = await Db.Opportunities.FirstOrDefaultAsync(item => item.Id == id);
        if (opportunity is null || !await CanAccessAsync(opportunity.AssignedToUserId)) return NotFoundOrForbidden();
        ApplyStageRules(input);
        Revalidate(input);
        await ValidateAccessAsync(input);
        if (!ModelState.IsValid)
        {
            await PopulateOptionsAsync(input);
            return View(input);
        }

        var oldValues = new { opportunity.Stage, opportunity.Amount, opportunity.Probability, opportunity.ExpectedCloseDate, opportunity.AssignedToUserId };
        Db.Entry(opportunity).Property(item => item.RowVersion).OriginalValue = input.RowVersion;
        opportunity.Name = input.Name;
        opportunity.Description = input.Description;
        opportunity.CustomerId = input.CustomerId;
        opportunity.LeadId = input.LeadId;
        opportunity.Stage = input.Stage;
        opportunity.Amount = input.Amount;
        opportunity.Probability = input.Probability;
        opportunity.ExpectedCloseDate = input.ExpectedCloseDate;
        opportunity.ActualCloseDate = input.ActualCloseDate;
        opportunity.AssignedToUserId = input.AssignedToUserId;
        try { await Db.SaveChangesAsync(); }
        catch (DbUpdateConcurrencyException)
        {
            ModelState.AddModelError(string.Empty, "This opportunity was changed by another user. Reload and try again.");
            await PopulateOptionsAsync(input);
            return View(input);
        }
        await _audit.LogAsync(oldValues.Stage == opportunity.Stage ? AuditAction.Updated : AuditAction.StageChanged, "Opportunity", opportunity.Id.ToString(), $"Updated opportunity {opportunity.Name}.", oldValues, new { opportunity.Stage, opportunity.Amount, opportunity.Probability, opportunity.ExpectedCloseDate, opportunity.AssignedToUserId });
        TempData["SuccessMessage"] = "Opportunity updated successfully.";
        return RedirectToAction(nameof(Details), new { id = opportunity.Id });
    }

    public async Task<IActionResult> Delete(int? id)
    {
        if (id is null) return NotFound();
        var opportunity = await Db.Opportunities.Include(item => item.Customer).Include(item => item.Lead).FirstOrDefaultAsync(item => item.Id == id.Value);
        if (opportunity is null || !await CanAccessAsync(opportunity.AssignedToUserId)) return NotFoundOrForbidden();
        return View(opportunity);
    }

    [HttpPost, ActionName("Delete")]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var opportunity = await Db.Opportunities.FindAsync(id);
        if (opportunity is null || !await CanAccessAsync(opportunity.AssignedToUserId)) return NotFoundOrForbidden();
        var name = opportunity.Name;
        Db.Opportunities.Remove(opportunity);
        try { await Db.SaveChangesAsync(); }
        catch (DbUpdateException)
        {
            TempData["ErrorMessage"] = "This opportunity has activities and cannot be deleted. Keep it as historical sales data instead.";
            return RedirectToAction(nameof(Details), new { id });
        }
        await _audit.LogAsync(AuditAction.Deleted, "Opportunity", id.ToString(), $"Deleted opportunity {name}.");
        TempData["SuccessMessage"] = "Opportunity deleted.";
        return RedirectToAction(nameof(Index));
    }

    private void ApplyStageRules(Opportunity opportunity)
    {
        if (opportunity.Stage == OpportunityStage.ClosedWon)
        {
            opportunity.Probability = 100;
            opportunity.ActualCloseDate ??= DateTime.UtcNow;
        }
        else if (opportunity.Stage == OpportunityStage.ClosedLost)
        {
            opportunity.Probability = 0;
            opportunity.ActualCloseDate ??= DateTime.UtcNow;
        }
        else
        {
            opportunity.ActualCloseDate = null;
        }
    }

    private void Revalidate(Opportunity opportunity)
    {
        // Stage changes can deterministically set probability/actual close date.
        // Re-run validation after those business rules rather than accepting a stale
        // model-binding error for values the server intentionally normalized.
        ModelState.Clear();
        TryValidateModel(opportunity);
    }

    private async Task ValidateAccessAsync(Opportunity opportunity)
    {
        if (!await CanAssignToAsync(opportunity.AssignedToUserId)) ModelState.AddModelError(nameof(opportunity.AssignedToUserId), "You can only assign an opportunity within your accessible team.");
        if (opportunity.CustomerId is not null)
        {
            var customer = await Db.Customers.AsNoTracking().FirstOrDefaultAsync(item => item.Id == opportunity.CustomerId.Value);
            if (customer is null || !await CanAccessAsync(customer.OwnerId)) ModelState.AddModelError(nameof(opportunity.CustomerId), "Choose a customer you can access.");
        }
        if (opportunity.LeadId is not null)
        {
            var lead = await Db.Leads.AsNoTracking().FirstOrDefaultAsync(item => item.Id == opportunity.LeadId.Value);
            if (lead is null || !await CanAccessAsync(lead.AssignedToUserId)) ModelState.AddModelError(nameof(opportunity.LeadId), "Choose a lead you can access.");
        }
    }

    private async Task PopulateOptionsAsync(Opportunity model)
    {
        var ids = await GetVisibleUserIdsAsync();
        var customers = Db.Customers.AsNoTracking();
        var leads = Db.Leads.AsNoTracking();
        var users = Db.Users.AsNoTracking().Where(user => user.IsActive);
        if (ids is not null)
        {
            customers = customers.Where(item => ids.Contains(item.OwnerId));
            leads = leads.Where(item => ids.Contains(item.AssignedToUserId));
            users = users.Where(item => ids.Contains(item.Id));
        }
        ViewData["Customers"] = new SelectList(await customers.OrderBy(item => item.CompanyName).ThenBy(item => item.LastName).Select(item => new { item.Id, Name = item.CompanyName ?? (item.FirstName + " " + item.LastName) }).ToListAsync(), "Id", "Name", model.CustomerId);
        ViewData["Leads"] = new SelectList(await leads.Where(item => item.Status != LeadStatus.Lost).OrderBy(item => item.CompanyName).ThenBy(item => item.LastName).Select(item => new { item.Id, Name = item.CompanyName ?? (item.FirstName + " " + item.LastName) }).ToListAsync(), "Id", "Name", model.LeadId);
        ViewData["AssignableUsers"] = new SelectList(await users.OrderBy(item => item.FirstName).ThenBy(item => item.LastName).Select(item => new { item.Id, Name = item.FirstName + " " + item.LastName }).ToListAsync(), "Id", "Name", model.AssignedToUserId);
    }
}
