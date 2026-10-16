using AcxiomCRM.Data;
using AcxiomCRM.Models;
using AcxiomCRM.Services;
using AcxiomCRM.ViewModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Controllers.Api;

[Route("api/opportunities")]
public sealed class OpportunitiesApiController : CrmApiControllerBase
{
    public OpportunitiesApiController(ApplicationDbContext db, IDataScopeService scope, UserManager<ApplicationUser> users, IAuditService audit) : base(db, scope, users, audit) { }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<OpportunityDto>>> GetAll([FromQuery] OpportunityStage? stage)
    {
        var ids = await AccessibleIdsAsync();
        var query = Db.Opportunities.AsNoTracking().Where(item => ids.Contains(item.AssignedToUserId));
        if (stage is not null) query = query.Where(item => item.Stage == stage);
        var items = await query.OrderBy(item => item.ExpectedCloseDate).ToListAsync();
        return Ok(items.Select(ToDto));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<OpportunityDto>> Get(int id)
    {
        var item = await Db.Opportunities.AsNoTracking().FirstOrDefaultAsync(opportunity => opportunity.Id == id);
        if (item is null || !await CanAccessAsync(item.AssignedToUserId)) return NotFound();
        return Ok(ToDto(item));
    }

    [HttpPost]
    public async Task<ActionResult<OpportunityDto>> Create(OpportunityUpsertDto input)
    {
        var validation = await ValidateInputAsync(input, null);
        if (validation is not null) return validation;
        var item = FromInput(input, string.IsNullOrWhiteSpace(input.AssignedToUserId) ? CurrentUserId : input.AssignedToUserId!);
        Db.Opportunities.Add(item);
        await Db.SaveChangesAsync();
        await Audit.LogAsync(AuditAction.Created, "Opportunity", item.Id.ToString(), "Created through REST API.", newValues: new { item.Stage, item.Amount, item.AssignedToUserId }, cancellationToken: HttpContext.RequestAborted);
        return CreatedAtAction(nameof(Get), new { id = item.Id }, ToDto(item));
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<OpportunityDto>> Update(int id, OpportunityUpsertDto input)
    {
        var item = await Db.Opportunities.FirstOrDefaultAsync(opportunity => opportunity.Id == id);
        if (item is null || !await CanAccessAsync(item.AssignedToUserId)) return NotFound();
        var validation = await ValidateInputAsync(input, item.AssignedToUserId);
        if (validation is not null) return validation;
        var update = FromInput(input, string.IsNullOrWhiteSpace(input.AssignedToUserId) ? item.AssignedToUserId : input.AssignedToUserId!);
        item.Name = update.Name; item.Description = update.Description; item.CustomerId = update.CustomerId; item.LeadId = update.LeadId; item.Stage = update.Stage; item.Amount = update.Amount; item.Probability = update.Probability; item.ExpectedCloseDate = update.ExpectedCloseDate; item.ActualCloseDate = update.ActualCloseDate; item.AssignedToUserId = update.AssignedToUserId;
        await Db.SaveChangesAsync();
        await Audit.LogAsync(AuditAction.Updated, "Opportunity", item.Id.ToString(), "Updated through REST API.", newValues: new { item.Stage, item.Amount, item.AssignedToUserId }, cancellationToken: HttpContext.RequestAborted);
        return Ok(ToDto(item));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var item = await Db.Opportunities.FirstOrDefaultAsync(opportunity => opportunity.Id == id);
        if (item is null || !await CanAccessAsync(item.AssignedToUserId)) return NotFound();
        Db.Opportunities.Remove(item);
        try { await Db.SaveChangesAsync(); }
        catch (DbUpdateException) { return Conflict(new { message = "Opportunities with related activities cannot be deleted." }); }
        await Audit.LogAsync(AuditAction.Deleted, "Opportunity", id.ToString(), "Deleted through REST API.", cancellationToken: HttpContext.RequestAborted);
        return NoContent();
    }

    private async Task<ActionResult?> ValidateInputAsync(OpportunityUpsertDto input, string? currentAssignee)
    {
        if (input.CustomerId is null && input.LeadId is null) return BadRequest(new { message = "An opportunity must be linked to a customer or lead." });
        var assigneeId = string.IsNullOrWhiteSpace(input.AssignedToUserId) ? currentAssignee ?? CurrentUserId : input.AssignedToUserId;
        if (!await CanAccessAsync(assigneeId!)) return Forbid();
        if (input.CustomerId is not null)
        {
            var customer = await Db.Customers.AsNoTracking().FirstOrDefaultAsync(item => item.Id == input.CustomerId.Value);
            if (customer is null || !await CanAccessAsync(customer.OwnerId)) return BadRequest(new { message = "Customer is unavailable." });
        }
        if (input.LeadId is not null)
        {
            var lead = await Db.Leads.AsNoTracking().FirstOrDefaultAsync(item => item.Id == input.LeadId.Value);
            if (lead is null || !await CanAccessAsync(lead.AssignedToUserId)) return BadRequest(new { message = "Lead is unavailable." });
        }
        return null;
    }

    private static Opportunity FromInput(OpportunityUpsertDto input, string assigneeId)
    {
        var opportunity = new Opportunity { Name = input.Name.Trim(), Description = input.Description?.Trim(), CustomerId = input.CustomerId, LeadId = input.LeadId, Stage = input.Stage, Amount = input.Amount, Probability = input.Probability, ExpectedCloseDate = input.ExpectedCloseDate!.Value, AssignedToUserId = assigneeId };
        opportunity.UpdateStage(input.Stage, input.Stage is OpportunityStage.ClosedWon or OpportunityStage.ClosedLost ? DateTime.UtcNow : null);
        // Preserve the API-supplied probability for open stages after stage rules clear close-only values.
        if (!opportunity.IsClosed) opportunity.Probability = input.Probability;
        return opportunity;
    }

    private static OpportunityDto ToDto(Opportunity item) => new(item.Id, item.Name, item.CustomerId, item.LeadId, item.Stage, item.Amount, item.Probability, item.ExpectedCloseDate, item.ActualCloseDate, item.AssignedToUserId, item.CreatedAt);
}
