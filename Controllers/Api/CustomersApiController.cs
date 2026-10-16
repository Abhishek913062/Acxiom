using AcxiomCRM.Data;
using AcxiomCRM.Models;
using AcxiomCRM.Services;
using AcxiomCRM.ViewModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Controllers.Api;

[Route("api/customers")]
public sealed class CustomersApiController : CrmApiControllerBase
{
    public CustomersApiController(ApplicationDbContext db, IDataScopeService scope, UserManager<ApplicationUser> users, IAuditService audit) : base(db, scope, users, audit) { }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<CustomerDto>>> GetAll([FromQuery] string? search)
    {
        var ids = await AccessibleIdsAsync();
        var query = Db.Customers.AsNoTracking().Where(item => ids.Contains(item.OwnerId));
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(item => item.FirstName.Contains(term) || item.LastName.Contains(term) || item.Email.Contains(term) || (item.CompanyName != null && item.CompanyName.Contains(term)));
        }
        return Ok(await query.OrderByDescending(item => item.CreatedAt).Select(item => ToDto(item)).ToListAsync());
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<CustomerDto>> Get(int id)
    {
        var item = await Db.Customers.AsNoTracking().FirstOrDefaultAsync(customer => customer.Id == id);
        if (item is null || !await CanAccessAsync(item.OwnerId)) return NotFound();
        return Ok(ToDto(item));
    }

    [HttpPost]
    public async Task<ActionResult<CustomerDto>> Create(CustomerUpsertDto input)
    {
        var ownerId = string.IsNullOrWhiteSpace(input.OwnerId) ? CurrentUserId : input.OwnerId;
        if (!await CanAccessAsync(ownerId!)) return Forbid();
        var normalized = input.Email.Trim().ToUpperInvariant();
        if (await Db.Customers.AnyAsync(item => item.NormalizedEmail == normalized)) return Conflict(new { message = "A customer already exists with this email address." });
        var item = new Customer { FirstName = input.FirstName.Trim(), LastName = input.LastName.Trim(), CompanyName = input.CompanyName?.Trim(), Email = input.Email.Trim(), Phone = input.Phone?.Trim(), Status = input.Status, OwnerId = ownerId!, Notes = input.Notes?.Trim() };
        Db.Customers.Add(item);
        try { await Db.SaveChangesAsync(); }
        catch (DbUpdateException) { return Conflict(new { message = "A customer already exists with this email address." }); }
        await Audit.LogAsync(AuditAction.Created, "Customer", item.Id.ToString(), "Created through REST API.", newValues: new { item.Email, item.OwnerId }, cancellationToken: HttpContext.RequestAborted);
        return CreatedAtAction(nameof(Get), new { id = item.Id }, ToDto(item));
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<CustomerDto>> Update(int id, CustomerUpsertDto input)
    {
        var item = await Db.Customers.FirstOrDefaultAsync(customer => customer.Id == id);
        if (item is null || !await CanAccessAsync(item.OwnerId)) return NotFound();
        var ownerId = string.IsNullOrWhiteSpace(input.OwnerId) ? item.OwnerId : input.OwnerId;
        if (!await CanAccessAsync(ownerId!)) return Forbid();
        var normalized = input.Email.Trim().ToUpperInvariant();
        if (await Db.Customers.AnyAsync(customer => customer.NormalizedEmail == normalized && customer.Id != id)) return Conflict(new { message = "A customer already exists with this email address." });
        item.FirstName = input.FirstName.Trim(); item.LastName = input.LastName.Trim(); item.CompanyName = input.CompanyName?.Trim(); item.Email = input.Email.Trim(); item.Phone = input.Phone?.Trim(); item.Status = input.Status; item.OwnerId = ownerId!; item.Notes = input.Notes?.Trim();
        try { await Db.SaveChangesAsync(); }
        catch (DbUpdateException) { return Conflict(new { message = "A customer already exists with this email address." }); }
        await Audit.LogAsync(AuditAction.Updated, "Customer", item.Id.ToString(), "Updated through REST API.", newValues: new { item.Email, item.OwnerId }, cancellationToken: HttpContext.RequestAborted);
        return Ok(ToDto(item));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var item = await Db.Customers.FirstOrDefaultAsync(customer => customer.Id == id);
        if (item is null || !await CanAccessAsync(item.OwnerId)) return NotFound();
        Db.Customers.Remove(item);
        try { await Db.SaveChangesAsync(); }
        catch (DbUpdateException) { return Conflict(new { message = "Customers with related sales records cannot be deleted." }); }
        await Audit.LogAsync(AuditAction.Deleted, "Customer", id.ToString(), "Deleted through REST API.", cancellationToken: HttpContext.RequestAborted);
        return NoContent();
    }

    private static CustomerDto ToDto(Customer item) => new(item.Id, item.FirstName, item.LastName, item.CompanyName, item.Email, item.Phone, item.Status, item.OwnerId, item.CreatedAt);
}
