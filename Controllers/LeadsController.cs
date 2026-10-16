using AcxiomCRM.Data;
using AcxiomCRM.Models;
using AcxiomCRM.Services;
using AcxiomCRM.ViewModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Controllers;

public sealed class LeadsController : CrmControllerBase
{
    private readonly IAuditService _audit;

    public LeadsController(ApplicationDbContext db, UserManager<ApplicationUser> users, IDataScopeService scope, IAuditService audit)
        : base(db, users, scope) => _audit = audit;

    public async Task<IActionResult> Index(string? search, LeadStatus? status, LeadSource? source)
    {
        var ids = await GetVisibleUserIdsAsync();
        var query = Db.Leads.AsNoTracking().Include(lead => lead.AssignedToUser).AsQueryable();
        if (ids is not null) query = query.Where(lead => ids.Contains(lead.AssignedToUserId));
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(lead => lead.FirstName.Contains(term) || lead.LastName.Contains(term)
                || (lead.Email != null && lead.Email.Contains(term)) || (lead.CompanyName != null && lead.CompanyName.Contains(term)));
        }
        if (status is not null) query = query.Where(lead => lead.Status == status);
        if (source is not null) query = query.Where(lead => lead.Source == source);

        ViewData["Search"] = search;
        ViewData["Status"] = status;
        ViewData["Source"] = source;
        ViewData["Section"] = "Customer management";
        ViewData["PageTitle"] = "Leads";
        return View(await query.OrderByDescending(lead => lead.CreatedAt).ToListAsync());
    }

    public async Task<IActionResult> Details(int? id)
    {
        if (id is null) return NotFound();
        var lead = await Db.Leads.Include(item => item.AssignedToUser)
            .Include(item => item.ConvertedCustomer)
            .Include(item => item.Opportunities)
            .Include(item => item.Activities.OrderByDescending(activity => activity.CreatedAt).Take(10))
            .FirstOrDefaultAsync(item => item.Id == id.Value);
        if (lead is null || !await CanAccessAsync(lead.AssignedToUserId)) return NotFoundOrForbidden();
        ViewData["Section"] = "Customer management";
        ViewData["PageTitle"] = $"{lead.FirstName} {lead.LastName}";
        return View(lead);
    }

    public async Task<IActionResult> Create()
    {
        var model = new Lead { AssignedToUserId = CurrentUserId, NextFollowUpAt = DateTime.UtcNow.AddDays(1) };
        await PopulateAssignableUsersAsync(model.AssignedToUserId);
        ViewData["Section"] = "Customer management";
        ViewData["PageTitle"] = "Add lead";
        return View(model);
    }

    [HttpPost]
    public async Task<IActionResult> Create([Bind("FirstName,LastName,CompanyName,Email,Phone,JobTitle,Source,Status,Score,EstimatedValue,Notes,NextFollowUpAt,LastContactedAt,AssignedToUserId")] Lead lead)
    {
        ValidateLeadState(lead);
        if (!await CanAssignToAsync(lead.AssignedToUserId)) ModelState.AddModelError(nameof(lead.AssignedToUserId), "You can only assign a lead within your accessible team.");
        if (!ModelState.IsValid)
        {
            await PopulateAssignableUsersAsync(lead.AssignedToUserId);
            return View(lead);
        }

        Db.Leads.Add(lead);
        await Db.SaveChangesAsync();
        await _audit.LogAsync(AuditAction.Created, "Lead", lead.Id.ToString(), $"Created lead {lead.FirstName} {lead.LastName}.", newValues: new { lead.Email, lead.Status, lead.Source, lead.AssignedToUserId });
        TempData["SuccessMessage"] = "Lead created successfully.";
        return RedirectToAction(nameof(Details), new { id = lead.Id });
    }

    public async Task<IActionResult> Edit(int? id)
    {
        if (id is null) return NotFound();
        var lead = await Db.Leads.FindAsync(id.Value);
        if (lead is null || !await CanAccessAsync(lead.AssignedToUserId)) return NotFoundOrForbidden();
        if (lead.IsConverted)
        {
            TempData["InfoMessage"] = "Converted leads are preserved as historical records and cannot be edited.";
            return RedirectToAction(nameof(Details), new { id });
        }
        await PopulateAssignableUsersAsync(lead.AssignedToUserId);
        ViewData["Section"] = "Customer management";
        ViewData["PageTitle"] = "Edit lead";
        return View(lead);
    }

    [HttpPost]
    public async Task<IActionResult> Edit(int id, [Bind("Id,FirstName,LastName,CompanyName,Email,Phone,JobTitle,Source,Status,Score,EstimatedValue,Notes,NextFollowUpAt,LastContactedAt,AssignedToUserId,RowVersion")] Lead input)
    {
        if (id != input.Id) return NotFound();
        var lead = await Db.Leads.FirstOrDefaultAsync(item => item.Id == id);
        if (lead is null || !await CanAccessAsync(lead.AssignedToUserId)) return NotFoundOrForbidden();
        if (lead.IsConverted)
        {
            TempData["ErrorMessage"] = "Converted leads cannot be edited.";
            return RedirectToAction(nameof(Details), new { id });
        }

        ValidateLeadState(input);
        if (!await CanAssignToAsync(input.AssignedToUserId)) ModelState.AddModelError(nameof(input.AssignedToUserId), "You can only assign a lead within your accessible team.");
        if (!ModelState.IsValid)
        {
            await PopulateAssignableUsersAsync(input.AssignedToUserId);
            return View(input);
        }

        var oldValues = new { lead.Status, lead.Score, lead.AssignedToUserId, lead.NextFollowUpAt };
        Db.Entry(lead).Property(item => item.RowVersion).OriginalValue = input.RowVersion;
        lead.FirstName = input.FirstName;
        lead.LastName = input.LastName;
        lead.CompanyName = input.CompanyName;
        lead.Email = input.Email?.Trim();
        lead.Phone = input.Phone;
        lead.JobTitle = input.JobTitle;
        lead.Source = input.Source;
        lead.Status = input.Status;
        lead.Score = input.Score;
        lead.EstimatedValue = input.EstimatedValue;
        lead.Notes = input.Notes;
        lead.NextFollowUpAt = input.NextFollowUpAt;
        lead.LastContactedAt = input.LastContactedAt;
        lead.AssignedToUserId = input.AssignedToUserId;
        try { await Db.SaveChangesAsync(); }
        catch (DbUpdateConcurrencyException)
        {
            ModelState.AddModelError(string.Empty, "This lead changed while you were editing it. Reload the page and try again.");
            await PopulateAssignableUsersAsync(input.AssignedToUserId);
            return View(input);
        }
        await _audit.LogAsync(AuditAction.Updated, "Lead", lead.Id.ToString(), $"Updated lead {lead.FirstName} {lead.LastName}.", oldValues, new { lead.Status, lead.Score, lead.AssignedToUserId, lead.NextFollowUpAt });
        TempData["SuccessMessage"] = "Lead updated successfully.";
        return RedirectToAction(nameof(Details), new { id = lead.Id });
    }

    public async Task<IActionResult> Convert(int? id)
    {
        if (id is null) return NotFound();
        var lead = await Db.Leads.FindAsync(id.Value);
        if (lead is null || !await CanAccessAsync(lead.AssignedToUserId)) return NotFoundOrForbidden();
        if (lead.IsConverted || lead.Status == LeadStatus.Lost)
        {
            TempData["ErrorMessage"] = lead.IsConverted ? "This lead has already been converted." : "Lost leads cannot be converted.";
            return RedirectToAction(nameof(Details), new { id });
        }

        await PopulateAssignableUsersAsync(lead.AssignedToUserId);
        return View(new LeadConversionViewModel
        {
            LeadId = lead.Id,
            LeadName = $"{lead.FirstName} {lead.LastName}",
            FirstName = lead.FirstName,
            LastName = lead.LastName,
            CompanyName = lead.CompanyName,
            Email = lead.Email ?? string.Empty,
            Phone = lead.Phone,
            OwnerId = lead.AssignedToUserId,
            Notes = lead.Notes
        });
    }

    [HttpPost]
    public async Task<IActionResult> Convert(LeadConversionViewModel input)
    {
        var lead = await Db.Leads.FirstOrDefaultAsync(item => item.Id == input.LeadId);
        if (lead is null || !await CanAccessAsync(lead.AssignedToUserId)) return NotFoundOrForbidden();
        if (lead.IsConverted || lead.Status == LeadStatus.Lost)
        {
            TempData["ErrorMessage"] = "This lead is no longer eligible for conversion.";
            return RedirectToAction(nameof(Details), new { id = input.LeadId });
        }
        input.Email = input.Email?.Trim() ?? string.Empty;
        if (!await CanAssignToAsync(input.OwnerId)) ModelState.AddModelError(nameof(input.OwnerId), "You can only assign a customer within your accessible team.");
        var normalizedEmail = input.Email.ToUpperInvariant();
        if (await Db.Customers.AnyAsync(customer => customer.NormalizedEmail == normalizedEmail)) ModelState.AddModelError(nameof(input.Email), "A customer already exists with this email address.");
        if (!ModelState.IsValid)
        {
            input.LeadName = $"{lead.FirstName} {lead.LastName}";
            await PopulateAssignableUsersAsync(input.OwnerId);
            return View(input);
        }

        var customer = new Customer
        {
            FirstName = input.FirstName,
            LastName = input.LastName,
            CompanyName = input.CompanyName,
            Email = input.Email,
            Phone = input.Phone,
            OwnerId = input.OwnerId,
            Status = input.Status,
            Notes = input.Notes
        };
        try
        {
            await using var transaction = await Db.Database.BeginTransactionAsync();
            Db.Customers.Add(customer);
            await Db.SaveChangesAsync();
            lead.MarkConverted(customer.Id, DateTime.UtcNow);
            await Db.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        catch (DbUpdateException)
        {
            Db.ChangeTracker.Clear();
            ModelState.AddModelError(nameof(input.Email), "The customer could not be created because this email is already in use.");
            input.LeadName = $"{lead.FirstName} {lead.LastName}";
            await PopulateAssignableUsersAsync(input.OwnerId);
            return View(input);
        }
        await _audit.LogAsync(AuditAction.Converted, "Lead", lead.Id.ToString(), $"Converted lead {lead.FirstName} {lead.LastName} to customer {customer.Id}.", newValues: new { customer.Id, customer.Email, customer.OwnerId });
        TempData["SuccessMessage"] = "Lead converted to a customer successfully.";
        return RedirectToAction("Details", "Customers", new { id = customer.Id });
    }

    public async Task<IActionResult> Delete(int? id)
    {
        if (id is null) return NotFound();
        var lead = await Db.Leads.Include(item => item.AssignedToUser).FirstOrDefaultAsync(item => item.Id == id.Value);
        if (lead is null || !await CanAccessAsync(lead.AssignedToUserId)) return NotFoundOrForbidden();
        if (lead.IsConverted)
        {
            TempData["InfoMessage"] = "Converted lead history is retained and cannot be deleted.";
            return RedirectToAction(nameof(Details), new { id });
        }
        return View(lead);
    }

    [HttpPost, ActionName("Delete")]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var lead = await Db.Leads.FindAsync(id);
        if (lead is null || !await CanAccessAsync(lead.AssignedToUserId)) return NotFoundOrForbidden();
        if (lead.IsConverted) return RedirectToAction(nameof(Details), new { id });
        var name = $"{lead.FirstName} {lead.LastName}";
        Db.Leads.Remove(lead);
        try { await Db.SaveChangesAsync(); }
        catch (DbUpdateException)
        {
            TempData["ErrorMessage"] = "This lead has related opportunities or activities and cannot be deleted.";
            return RedirectToAction(nameof(Details), new { id });
        }
        await _audit.LogAsync(AuditAction.Deleted, "Lead", id.ToString(), $"Deleted lead {name}.");
        TempData["SuccessMessage"] = "Lead deleted.";
        return RedirectToAction(nameof(Index));
    }

    private void ValidateLeadState(Lead lead)
    {
        if (lead.Status == LeadStatus.Converted)
        {
            ModelState.AddModelError(nameof(lead.Status), "Use the dedicated conversion flow to convert a lead.");
        }
    }

    private async Task PopulateAssignableUsersAsync(string? selectedId)
    {
        var ids = await GetVisibleUserIdsAsync();
        var query = Db.Users.AsNoTracking().Where(user => user.IsActive);
        if (ids is not null) query = query.Where(user => ids.Contains(user.Id));
        var users = await query.OrderBy(user => user.FirstName).ThenBy(user => user.LastName)
            .Select(user => new { user.Id, Name = user.FirstName + " " + user.LastName + " (" + user.Email + ")" }).ToListAsync();
        ViewData["AssignableUsers"] = new SelectList(users, "Id", "Name", selectedId);
    }
}
