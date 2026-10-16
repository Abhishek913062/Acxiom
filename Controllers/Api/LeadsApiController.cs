using AcxiomCRM.Data;
using AcxiomCRM.Models;
using AcxiomCRM.Services;
using AcxiomCRM.ViewModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Controllers.Api;

[Route("api/leads")]
public sealed class LeadsApiController : CrmApiControllerBase
{
    public LeadsApiController(ApplicationDbContext db, IDataScopeService scope, UserManager<ApplicationUser> users, IAuditService audit) : base(db, scope, users, audit) { }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<LeadDto>>> GetAll([FromQuery] LeadStatus? status, [FromQuery] string? search)
    {
        var ids = await AccessibleIdsAsync();
        var query = Db.Leads.AsNoTracking().Where(item => ids.Contains(item.AssignedToUserId));
        if (status is not null) query = query.Where(item => item.Status == status);
        if (!string.IsNullOrWhiteSpace(search)) { var term = search.Trim(); query = query.Where(item => item.FirstName.Contains(term) || item.LastName.Contains(term) || (item.CompanyName != null && item.CompanyName.Contains(term))); }
        var items = await query.OrderByDescending(item => item.CreatedAt).ToListAsync();
        return Ok(items.Select(ToDto));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<LeadDto>> Get(int id)
    {
        var item = await Db.Leads.AsNoTracking().FirstOrDefaultAsync(lead => lead.Id == id);
        if (item is null || !await CanAccessAsync(item.AssignedToUserId)) return NotFound();
        return Ok(ToDto(item));
    }

    [HttpPost]
    public async Task<ActionResult<LeadDto>> Create(LeadUpsertDto input)
    {
        if (input.Status == LeadStatus.Converted) return BadRequest(new { message = "Use the lead conversion workflow to convert a lead." });
        var assigneeId = string.IsNullOrWhiteSpace(input.AssignedToUserId) ? CurrentUserId : input.AssignedToUserId;
        if (!await CanAccessAsync(assigneeId!)) return Forbid();
        var item = new Lead { FirstName = input.FirstName.Trim(), LastName = input.LastName.Trim(), CompanyName = input.CompanyName?.Trim(), Email = input.Email?.Trim(), Phone = input.Phone?.Trim(), JobTitle = input.JobTitle?.Trim(), Source = input.Source, Status = input.Status, Score = input.Score, EstimatedValue = input.EstimatedValue, NextFollowUpAt = input.NextFollowUpAt, AssignedToUserId = assigneeId!, Notes = input.Notes?.Trim() };
        Db.Leads.Add(item);
        await Db.SaveChangesAsync();
        await Audit.LogAsync(AuditAction.Created, "Lead", item.Id.ToString(), "Created through REST API.", newValues: new { item.Status, item.AssignedToUserId }, cancellationToken: HttpContext.RequestAborted);
        return CreatedAtAction(nameof(Get), new { id = item.Id }, ToDto(item));
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<LeadDto>> Update(int id, LeadUpsertDto input)
    {
        var item = await Db.Leads.FirstOrDefaultAsync(lead => lead.Id == id);
        if (item is null || !await CanAccessAsync(item.AssignedToUserId)) return NotFound();
        if (item.IsConverted || input.Status == LeadStatus.Converted) return BadRequest(new { message = "Converted leads are immutable. Use the conversion workflow." });
        var assigneeId = string.IsNullOrWhiteSpace(input.AssignedToUserId) ? item.AssignedToUserId : input.AssignedToUserId;
        if (!await CanAccessAsync(assigneeId!)) return Forbid();
        item.FirstName = input.FirstName.Trim(); item.LastName = input.LastName.Trim(); item.CompanyName = input.CompanyName?.Trim(); item.Email = input.Email?.Trim(); item.Phone = input.Phone?.Trim(); item.JobTitle = input.JobTitle?.Trim(); item.Source = input.Source; item.Status = input.Status; item.Score = input.Score; item.EstimatedValue = input.EstimatedValue; item.NextFollowUpAt = input.NextFollowUpAt; item.AssignedToUserId = assigneeId!; item.Notes = input.Notes?.Trim();
        await Db.SaveChangesAsync();
        await Audit.LogAsync(AuditAction.Updated, "Lead", item.Id.ToString(), "Updated through REST API.", newValues: new { item.Status, item.AssignedToUserId }, cancellationToken: HttpContext.RequestAborted);
        return Ok(ToDto(item));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var item = await Db.Leads.FirstOrDefaultAsync(lead => lead.Id == id);
        if (item is null || !await CanAccessAsync(item.AssignedToUserId)) return NotFound();
        if (item.IsConverted) return Conflict(new { message = "Converted lead history cannot be deleted." });
        Db.Leads.Remove(item);
        try { await Db.SaveChangesAsync(); }
        catch (DbUpdateException) { return Conflict(new { message = "Leads with related sales records cannot be deleted." }); }
        await Audit.LogAsync(AuditAction.Deleted, "Lead", id.ToString(), "Deleted through REST API.", cancellationToken: HttpContext.RequestAborted);
        return NoContent();
    }

    private static LeadDto ToDto(Lead item) => new(item.Id, item.FirstName, item.LastName, item.CompanyName, item.Email, item.Source, item.Status, item.Score, item.EstimatedValue, item.AssignedToUserId, item.CreatedAt);
}
