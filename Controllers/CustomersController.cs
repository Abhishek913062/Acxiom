using AcxiomCRM.Data;
using AcxiomCRM.Models;
using AcxiomCRM.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Controllers;

public sealed class CustomersController : CrmControllerBase
{
    private readonly IAuditService _auditService;

    public CustomersController(
        ApplicationDbContext db,
        UserManager<ApplicationUser> userManager,
        IDataScopeService dataScope,
        IAuditService auditService)
        : base(db, userManager, dataScope)
    {
        _auditService = auditService;
    }

    public async Task<IActionResult> Index(string? search, CustomerStatus? status)
    {
        var visibleIds = await GetVisibleUserIdsAsync();
        var query = Db.Customers.AsNoTracking().Include(customer => customer.Owner).AsQueryable();
        if (visibleIds is not null)
        {
            query = query.Where(customer => visibleIds.Contains(customer.OwnerId));
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(customer => customer.FirstName.Contains(term)
                || customer.LastName.Contains(term)
                || customer.Email.Contains(term)
                || (customer.CompanyName != null && customer.CompanyName.Contains(term)));
        }

        if (status is not null)
        {
            query = query.Where(customer => customer.Status == status);
        }

        ViewData["Search"] = search;
        ViewData["Status"] = status;
        ViewData["Section"] = "Customer management";
        ViewData["PageTitle"] = "Customers";
        return View(await query.OrderByDescending(customer => customer.CreatedAt).ToListAsync());
    }

    public async Task<IActionResult> Details(int? id)
    {
        if (id is null)
        {
            return NotFound();
        }

        var customer = await Db.Customers
            .Include(item => item.Owner)
            .Include(item => item.Opportunities)
            .Include(item => item.Activities.OrderByDescending(activity => activity.CreatedAt).Take(10))
            .FirstOrDefaultAsync(item => item.Id == id.Value);
        if (customer is null || !await CanAccessAsync(customer.OwnerId))
        {
            return NotFoundOrForbidden();
        }

        ViewData["Section"] = "Customer management";
        ViewData["PageTitle"] = customer.DisplayName;
        return View(customer);
    }

    public async Task<IActionResult> Create()
    {
        var model = new Customer { OwnerId = CurrentUserId, Status = CustomerStatus.Prospect };
        await PopulateAssignableUsersAsync(model.OwnerId);
        ViewData["Section"] = "Customer management";
        ViewData["PageTitle"] = "Add customer";
        return View(model);
    }

    [HttpPost]
    public async Task<IActionResult> Create([Bind("FirstName,LastName,CompanyName,Email,Phone,AddressLine1,AddressLine2,City,State,PostalCode,Country,Notes,Status,OwnerId")] Customer customer)
    {
        customer.Email = customer.Email?.Trim() ?? string.Empty;
        if (!await CanAssignToAsync(customer.OwnerId))
        {
            ModelState.AddModelError(nameof(customer.OwnerId), "You can only assign a customer within your accessible team.");
        }

        if (await CustomerEmailExistsAsync(customer.Email))
        {
            ModelState.AddModelError(nameof(customer.Email), "A customer already exists with this email address.");
        }

        if (!ModelState.IsValid)
        {
            await PopulateAssignableUsersAsync(customer.OwnerId);
            return View(customer);
        }

        Db.Customers.Add(customer);
        try
        {
            await Db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            ModelState.AddModelError(nameof(customer.Email), "This email address is already in use by another customer.");
            await PopulateAssignableUsersAsync(customer.OwnerId);
            return View(customer);
        }

        await _auditService.LogAsync(AuditAction.Created, "Customer", customer.Id.ToString(),
            $"Created customer {customer.DisplayName}.", newValues: new { customer.DisplayName, customer.Email, customer.OwnerId });
        TempData["SuccessMessage"] = "Customer created successfully.";
        return RedirectToAction(nameof(Details), new { id = customer.Id });
    }

    public async Task<IActionResult> Edit(int? id)
    {
        if (id is null)
        {
            return NotFound();
        }

        var customer = await Db.Customers.FindAsync(id.Value);
        if (customer is null || !await CanAccessAsync(customer.OwnerId))
        {
            return NotFoundOrForbidden();
        }

        await PopulateAssignableUsersAsync(customer.OwnerId);
        ViewData["Section"] = "Customer management";
        ViewData["PageTitle"] = $"Edit {customer.DisplayName}";
        return View(customer);
    }

    [HttpPost]
    public async Task<IActionResult> Edit(int id, [Bind("Id,FirstName,LastName,CompanyName,Email,Phone,AddressLine1,AddressLine2,City,State,PostalCode,Country,Notes,Status,OwnerId,RowVersion")] Customer input)
    {
        if (id != input.Id)
        {
            return NotFound();
        }

        var customer = await Db.Customers.FirstOrDefaultAsync(item => item.Id == id);
        if (customer is null || !await CanAccessAsync(customer.OwnerId))
        {
            return NotFoundOrForbidden();
        }

        input.Email = input.Email?.Trim() ?? string.Empty;
        if (!await CanAssignToAsync(input.OwnerId))
        {
            ModelState.AddModelError(nameof(input.OwnerId), "You can only assign a customer within your accessible team.");
        }

        if (await CustomerEmailExistsAsync(input.Email, id))
        {
            ModelState.AddModelError(nameof(input.Email), "A customer already exists with this email address.");
        }

        if (!ModelState.IsValid)
        {
            await PopulateAssignableUsersAsync(input.OwnerId);
            return View(input);
        }

        var oldValues = new { customer.DisplayName, customer.Email, customer.Status, customer.OwnerId };
        Db.Entry(customer).Property(item => item.RowVersion).OriginalValue = input.RowVersion;
        customer.FirstName = input.FirstName;
        customer.LastName = input.LastName;
        customer.CompanyName = input.CompanyName;
        customer.Email = input.Email;
        customer.Phone = input.Phone;
        customer.AddressLine1 = input.AddressLine1;
        customer.AddressLine2 = input.AddressLine2;
        customer.City = input.City;
        customer.State = input.State;
        customer.PostalCode = input.PostalCode;
        customer.Country = input.Country;
        customer.Notes = input.Notes;
        customer.Status = input.Status;
        customer.OwnerId = input.OwnerId;

        try
        {
            await Db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            ModelState.AddModelError(string.Empty, "This customer was changed by another user. Reload the page and try again.");
            await PopulateAssignableUsersAsync(input.OwnerId);
            return View(input);
        }
        catch (DbUpdateException)
        {
            ModelState.AddModelError(nameof(input.Email), "This email address is already in use by another customer.");
            await PopulateAssignableUsersAsync(input.OwnerId);
            return View(input);
        }

        await _auditService.LogAsync(AuditAction.Updated, "Customer", customer.Id.ToString(),
            $"Updated customer {customer.DisplayName}.", oldValues, new { customer.DisplayName, customer.Email, customer.Status, customer.OwnerId });
        TempData["SuccessMessage"] = "Customer updated successfully.";
        return RedirectToAction(nameof(Details), new { id = customer.Id });
    }

    public async Task<IActionResult> Delete(int? id)
    {
        if (id is null)
        {
            return NotFound();
        }

        var customer = await Db.Customers.Include(item => item.Owner).FirstOrDefaultAsync(item => item.Id == id.Value);
        if (customer is null || !await CanAccessAsync(customer.OwnerId))
        {
            return NotFoundOrForbidden();
        }

        ViewData["Section"] = "Customer management";
        ViewData["PageTitle"] = "Delete customer";
        return View(customer);
    }

    [HttpPost, ActionName("Delete")]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var customer = await Db.Customers.FindAsync(id);
        if (customer is null || !await CanAccessAsync(customer.OwnerId))
        {
            return NotFoundOrForbidden();
        }

        var label = customer.DisplayName;
        Db.Customers.Remove(customer);
        try
        {
            await Db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            TempData["ErrorMessage"] = "This customer has related opportunities or activities and cannot be deleted. Mark it inactive instead.";
            return RedirectToAction(nameof(Details), new { id });
        }

        await _auditService.LogAsync(AuditAction.Deleted, "Customer", id.ToString(), $"Deleted customer {label}.");
        TempData["SuccessMessage"] = "Customer deleted.";
        return RedirectToAction(nameof(Index));
    }

    private async Task<bool> CustomerEmailExistsAsync(string? email, int? excludeId = null)
    {
        var normalized = string.IsNullOrWhiteSpace(email) ? string.Empty : email.Trim().ToUpperInvariant();
        return await Db.Customers.AnyAsync(customer => customer.NormalizedEmail == normalized
            && (!excludeId.HasValue || customer.Id != excludeId.Value));
    }

    private async Task PopulateAssignableUsersAsync(string? selectedId)
    {
        var ids = await GetVisibleUserIdsAsync();
        var users = Db.Users.AsNoTracking().Where(user => user.IsActive);
        if (ids is not null)
        {
            users = users.Where(user => ids.Contains(user.Id));
        }

        var options = await users.OrderBy(user => user.FirstName).ThenBy(user => user.LastName)
            .Select(user => new { user.Id, Name = user.FirstName + " " + user.LastName + " (" + user.Email + ")" })
            .ToListAsync();
        ViewData["AssignableUsers"] = new SelectList(options, "Id", "Name", selectedId);
    }
}
