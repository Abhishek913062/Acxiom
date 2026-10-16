using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AcxiomCRM.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;

namespace AcxiomCRM.Services;

/// <summary>
/// Seeds only role definitions by default. An initial administrator is created
/// only when both SeedAdmin:Email and SeedAdmin:Password are explicitly supplied.
/// </summary>
public class CrmSeedService : ICrmSeedService
{
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IConfiguration _configuration;

    public CrmSeedService(
        RoleManager<IdentityRole> roleManager,
        UserManager<ApplicationUser> userManager,
        IConfiguration configuration)
    {
        _roleManager = roleManager;
        _userManager = userManager;
        _configuration = configuration;
    }

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        foreach (var roleName in ApplicationRoles.All)
        {
            if (await _roleManager.RoleExistsAsync(roleName))
            {
                continue;
            }

            var result = await _roleManager.CreateAsync(new IdentityRole(roleName));
            EnsureSucceeded(result, $"create the '{roleName}' role");
        }

        cancellationToken.ThrowIfCancellationRequested();

        var email = _configuration["SeedAdmin:Email"]?.Trim();
        var password = _configuration["SeedAdmin:Password"];
        if (string.IsNullOrWhiteSpace(email) && string.IsNullOrWhiteSpace(password))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            throw new InvalidOperationException(
                "SeedAdmin:Email and SeedAdmin:Password must both be configured to seed an administrator.");
        }

        var administrator = await _userManager.FindByEmailAsync(email);
        if (administrator is null)
        {
            administrator = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                FirstName = _configuration["SeedAdmin:FirstName"]?.Trim() ?? "System",
                LastName = _configuration["SeedAdmin:LastName"]?.Trim() ?? "Administrator",
                JobTitle = "Administrator",
                IsActive = true,
                LockoutEnabled = true
            };

            var createResult = await _userManager.CreateAsync(administrator, password);
            EnsureSucceeded(createResult, "create the configured administrator");
        }

        if (!await _userManager.IsInRoleAsync(administrator, ApplicationRoles.Admin))
        {
            var roleResult = await _userManager.AddToRoleAsync(administrator, ApplicationRoles.Admin);
            EnsureSucceeded(roleResult, "assign the administrator role");
        }
    }

    private static void EnsureSucceeded(IdentityResult result, string action)
    {
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                $"Unable to {action}: {string.Join("; ", result.Errors.Select(error => error.Description))}");
        }
    }
}
