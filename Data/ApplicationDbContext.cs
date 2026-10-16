using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AcxiomCRM.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Data;

/// <summary>
/// Identity-aware EF Core context for all CRM data. It centralizes timestamps,
/// normalized e-mail values, validation, relational integrity, and concurrency.
/// </summary>
public class ApplicationDbContext : IdentityDbContext<ApplicationUser, IdentityRole, string>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Customer> Customers => Set<Customer>();

    public DbSet<Lead> Leads => Set<Lead>();

    public DbSet<Opportunity> Opportunities => Set<Opportunity>();

    public DbSet<Activity> Activities => Set<Activity>();

    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        ConfigureIdentity(builder);
        ConfigureCustomers(builder);
        ConfigureLeads(builder);
        ConfigureOpportunities(builder);
        ConfigureActivities(builder);
        ConfigureAuditLogs(builder);
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        PrepareChanges();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        PrepareChanges();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private static void ConfigureIdentity(ModelBuilder builder)
    {
        builder.Entity<ApplicationUser>(entity =>
        {
            entity.Property(user => user.FirstName).HasMaxLength(100).IsRequired();
            entity.Property(user => user.LastName).HasMaxLength(100).IsRequired();
            entity.Property(user => user.JobTitle).HasMaxLength(100);
            entity.Property(user => user.ManagerId).HasMaxLength(450);
            entity.HasIndex(user => user.ManagerId);

            entity.HasOne(user => user.Manager)
                .WithMany(manager => manager.DirectReports)
                .HasForeignKey(user => user.ManagerId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureCustomers(ModelBuilder builder)
    {
        builder.Entity<Customer>(entity =>
        {
            entity.ToTable("Customers", table =>
                table.HasCheckConstraint("CK_Customers_Email_NotBlank", "LEN(LTRIM(RTRIM([Email]))) > 0"));

            entity.Property(customer => customer.Email).HasMaxLength(256).IsRequired();
            entity.Property(customer => customer.NormalizedEmail).HasMaxLength(256).IsRequired();
            entity.Property(customer => customer.Status).HasConversion<string>().HasMaxLength(25);
            ConfigureAuditableEntity(entity);

            // A case- and whitespace-insensitive e-mail index is the final safety net
            // against concurrent attempts to create the same customer.
            entity.HasIndex(customer => customer.NormalizedEmail)
                .IsUnique()
                .HasDatabaseName("UX_Customers_NormalizedEmail");
            entity.HasIndex(customer => customer.OwnerId);
            entity.HasIndex(customer => new { customer.LastName, customer.FirstName });

            entity.HasOne(customer => customer.Owner)
                .WithMany(user => user.OwnedCustomers)
                .HasForeignKey(customer => customer.OwnerId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureLeads(ModelBuilder builder)
    {
        builder.Entity<Lead>(entity =>
        {
            entity.ToTable("Leads", table =>
            {
                table.HasCheckConstraint("CK_Leads_Score", "[Score] >= 0 AND [Score] <= 100");
                table.HasCheckConstraint(
                    "CK_Leads_Conversion",
                    "([Status] = 'Converted' AND [ConvertedCustomerId] IS NOT NULL AND [ConvertedAt] IS NOT NULL) OR " +
                    "([Status] <> 'Converted' AND [ConvertedCustomerId] IS NULL AND [ConvertedAt] IS NULL)");
            });

            entity.Property(lead => lead.Email).HasMaxLength(256);
            entity.Property(lead => lead.NormalizedEmail).HasMaxLength(256);
            entity.Property(lead => lead.EstimatedValue).HasPrecision(18, 2);
            entity.Property(lead => lead.Status).HasConversion<string>().HasMaxLength(25);
            entity.Property(lead => lead.Source).HasConversion<string>().HasMaxLength(25);
            ConfigureAuditableEntity(entity);

            entity.HasIndex(lead => new { lead.AssignedToUserId, lead.Status });
            entity.HasIndex(lead => lead.NormalizedEmail);
            entity.HasIndex(lead => lead.NextFollowUpAt);
            entity.HasIndex(lead => lead.ConvertedCustomerId)
                .IsUnique()
                .HasFilter("[ConvertedCustomerId] IS NOT NULL")
                .HasDatabaseName("UX_Leads_ConvertedCustomerId");

            entity.HasOne(lead => lead.AssignedToUser)
                .WithMany(user => user.AssignedLeads)
                .HasForeignKey(lead => lead.AssignedToUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(lead => lead.ConvertedCustomer)
                .WithOne(customer => customer.ConvertedFromLead)
                .HasForeignKey<Lead>(lead => lead.ConvertedCustomerId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureOpportunities(ModelBuilder builder)
    {
        builder.Entity<Opportunity>(entity =>
        {
            entity.ToTable("Opportunities", table =>
            {
                table.HasCheckConstraint("CK_Opportunities_Amount", "[Amount] > 0");
                table.HasCheckConstraint("CK_Opportunities_Probability", "[Probability] >= 0 AND [Probability] <= 100");
                table.HasCheckConstraint(
                    "CK_Opportunities_RelatedRecord",
                    "[CustomerId] IS NOT NULL OR [LeadId] IS NOT NULL");
                table.HasCheckConstraint(
                    "CK_Opportunities_ClosedStage",
                    "([Stage] IN ('ClosedWon', 'ClosedLost') AND [ActualCloseDate] IS NOT NULL) OR " +
                    "([Stage] NOT IN ('ClosedWon', 'ClosedLost') AND [ActualCloseDate] IS NULL)");
                table.HasCheckConstraint(
                    "CK_Opportunities_ClosedProbability",
                    "([Stage] <> 'ClosedWon' OR [Probability] = 100) AND " +
                    "([Stage] <> 'ClosedLost' OR [Probability] = 0)");
            });

            entity.Property(opportunity => opportunity.Amount).HasPrecision(18, 2);
            entity.Property(opportunity => opportunity.Stage).HasConversion<string>().HasMaxLength(25);
            ConfigureAuditableEntity(entity);

            entity.HasIndex(opportunity => new { opportunity.AssignedToUserId, opportunity.Stage });
            entity.HasIndex(opportunity => opportunity.ExpectedCloseDate);
            entity.HasIndex(opportunity => opportunity.CustomerId);
            entity.HasIndex(opportunity => opportunity.LeadId);

            entity.HasOne(opportunity => opportunity.Customer)
                .WithMany(customer => customer.Opportunities)
                .HasForeignKey(opportunity => opportunity.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(opportunity => opportunity.Lead)
                .WithMany(lead => lead.Opportunities)
                .HasForeignKey(opportunity => opportunity.LeadId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(opportunity => opportunity.AssignedToUser)
                .WithMany(user => user.AssignedOpportunities)
                .HasForeignKey(opportunity => opportunity.AssignedToUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureActivities(ModelBuilder builder)
    {
        builder.Entity<Activity>(entity =>
        {
            entity.ToTable("Activities", table =>
            {
                table.HasCheckConstraint(
                    "CK_Activities_RelatedRecord",
                    "[CustomerId] IS NOT NULL OR [LeadId] IS NOT NULL OR [OpportunityId] IS NOT NULL");
                table.HasCheckConstraint(
                    "CK_Activities_Completed",
                    "([Status] = 'Completed' AND [CompletedAt] IS NOT NULL) OR " +
                    "([Status] <> 'Completed' AND [CompletedAt] IS NULL)");
            });

            entity.Property(activity => activity.Type).HasConversion<string>().HasMaxLength(25);
            entity.Property(activity => activity.Status).HasConversion<string>().HasMaxLength(25);
            entity.Property(activity => activity.Priority).HasConversion<string>().HasMaxLength(25);
            ConfigureAuditableEntity(entity);

            entity.HasIndex(activity => new { activity.AssignedToUserId, activity.Status, activity.DueAt });
            entity.HasIndex(activity => activity.CustomerId);
            entity.HasIndex(activity => activity.LeadId);
            entity.HasIndex(activity => activity.OpportunityId);

            entity.HasOne(activity => activity.Customer)
                .WithMany(customer => customer.Activities)
                .HasForeignKey(activity => activity.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(activity => activity.Lead)
                .WithMany(lead => lead.Activities)
                .HasForeignKey(activity => activity.LeadId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(activity => activity.Opportunity)
                .WithMany(opportunity => opportunity.Activities)
                .HasForeignKey(activity => activity.OpportunityId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(activity => activity.AssignedToUser)
                .WithMany(user => user.AssignedActivities)
                .HasForeignKey(activity => activity.AssignedToUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureAuditLogs(ModelBuilder builder)
    {
        builder.Entity<AuditLog>(entity =>
        {
            entity.ToTable("AuditLogs");
            entity.HasKey(log => log.Id);
            entity.Property(log => log.Action).HasConversion<string>().HasMaxLength(30);
            entity.Property(log => log.EntityName).HasMaxLength(100).IsRequired();
            entity.Property(log => log.EntityId).HasMaxLength(128);
            entity.Property(log => log.Details).HasMaxLength(2000);
            entity.Property(log => log.UserId).HasMaxLength(450);
            entity.Property(log => log.IpAddress).HasMaxLength(64);
            entity.Property(log => log.OldValues).HasColumnType("nvarchar(max)");
            entity.Property(log => log.NewValues).HasColumnType("nvarchar(max)");
            entity.HasIndex(log => new { log.EntityName, log.EntityId, log.Timestamp });
            entity.HasIndex(log => new { log.UserId, log.Timestamp });

            entity.HasOne(log => log.User)
                .WithMany(user => user.AuditLogs)
                .HasForeignKey(log => log.UserId)
                .OnDelete(DeleteBehavior.SetNull);
        });
    }

    private static void ConfigureAuditableEntity<TEntity>(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<TEntity> entity)
        where TEntity : AuditableEntity
    {
        entity.Property(record => record.CreatedAt).IsRequired();
        entity.Property(record => record.UpdatedAt);
        entity.Property(record => record.RowVersion).IsRowVersion();
    }

    private void PrepareChanges()
    {
        var now = DateTime.UtcNow;

        foreach (var entry in ChangeTracker.Entries<AuditableEntity>())
        {
            if (entry.State == EntityState.Added)
            {
                if (entry.Entity.CreatedAt == default)
                {
                    entry.Entity.CreatedAt = now;
                }

                entry.Entity.UpdatedAt = now;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Property(record => record.CreatedAt).IsModified = false;
                entry.Entity.UpdatedAt = now;
            }
        }

        foreach (var entry in ChangeTracker.Entries<Customer>())
        {
            if (entry.State is EntityState.Added or EntityState.Modified)
            {
                entry.Entity.Email = entry.Entity.Email?.Trim() ?? string.Empty;
                entry.Entity.NormalizedEmail = Normalize(entry.Entity.Email) ?? string.Empty;
            }
        }

        foreach (var entry in ChangeTracker.Entries<Lead>())
        {
            if (entry.State is EntityState.Added or EntityState.Modified)
            {
                entry.Entity.Email = entry.Entity.Email?.Trim();
                entry.Entity.NormalizedEmail = Normalize(entry.Entity.Email);
            }
        }

        ValidateTrackedEntities();
    }

    private void ValidateTrackedEntities()
    {
        var failures = new List<ValidationResult>();

        foreach (var entry in ChangeTracker.Entries()
                     .Where(entry => entry.State is EntityState.Added or EntityState.Modified))
        {
            var validationResults = new List<ValidationResult>();
            var context = new ValidationContext(entry.Entity);

            if (!Validator.TryValidateObject(entry.Entity, context, validationResults, validateAllProperties: true))
            {
                failures.AddRange(validationResults);
            }
        }

        if (failures.Count > 0)
        {
            throw new ValidationException(string.Join(" ", failures.Select(failure => failure.ErrorMessage)));
        }
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToUpperInvariant();
}
