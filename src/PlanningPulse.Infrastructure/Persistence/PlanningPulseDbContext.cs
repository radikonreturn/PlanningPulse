using Microsoft.EntityFrameworkCore;
using PlanningPulse.Application.Tenancy;
using PlanningPulse.Domain.Boms;
using PlanningPulse.Domain.Common;
using PlanningPulse.Domain.Identity;
using PlanningPulse.Domain.Inventory;
using PlanningPulse.Domain.Items;
using PlanningPulse.Domain.Production;
using PlanningPulse.Domain.Routings;
using PlanningPulse.Domain.Suppliers;
using PlanningPulse.Domain.Tenancy;

namespace PlanningPulse.Infrastructure.Persistence;

public sealed class PlanningPulseDbContext(DbContextOptions<PlanningPulseDbContext> options, ICurrentTenant currentTenant) : DbContext(options)
{
    private readonly ICurrentTenant _currentTenant = currentTenant;

    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<ApplicationUser> Users => Set<ApplicationUser>();
    public DbSet<TenantUser> TenantUsers => Set<TenantUser>();
    public DbSet<Item> Items => Set<Item>();
    public DbSet<Bom> Boms => Set<Bom>();
    public DbSet<BomLine> BomLines => Set<BomLine>();
    public DbSet<WorkCenter> WorkCenters => Set<WorkCenter>();
    public DbSet<Routing> Routings => Set<Routing>();
    public DbSet<Operation> Operations => Set<Operation>();
    public DbSet<ProductionOrder> ProductionOrders => Set<ProductionOrder>();
    public DbSet<ScheduledOperation> ScheduledOperations => Set<ScheduledOperation>();
    public DbSet<InventoryLevel> InventoryLevels => Set<InventoryLevel>();
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<LeadTime> LeadTimes => Set<LeadTime>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PlanningPulseDbContext).Assembly);

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (typeof(ITenantOwned).IsAssignableFrom(entityType.ClrType))
            {
                var method = typeof(PlanningPulseDbContext)
                    .GetMethod(nameof(SetTenantFilter), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!
                    .MakeGenericMethod(entityType.ClrType);
                method.Invoke(null, [modelBuilder, this]);
            }
        }
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        StampTenantAndAuditFields();
        return base.SaveChangesAsync(cancellationToken);
    }

    public override int SaveChanges()
    {
        StampTenantAndAuditFields();
        return base.SaveChanges();
    }

    private void StampTenantAndAuditFields()
    {
        var now = DateTimeOffset.UtcNow;

        foreach (var entry in ChangeTracker.Entries<Entity>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAtUtc = now;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAtUtc = now;
            }

            if (entry.State == EntityState.Added && entry.Entity is ITenantOwned tenantOwned)
            {
                if (!_currentTenant.TenantId.HasValue && tenantOwned.TenantId == Guid.Empty)
                {
                    throw new InvalidOperationException("Tenant-owned data cannot be saved without a tenant context.");
                }

                if (tenantOwned.TenantId == Guid.Empty)
                {
                    tenantOwned.TenantId = _currentTenant.TenantId!.Value;
                }
            }
        }
    }

    private static void SetTenantFilter<TEntity>(ModelBuilder modelBuilder, PlanningPulseDbContext context)
        where TEntity : class, ITenantOwned
    {
        modelBuilder.Entity<TEntity>().HasQueryFilter(entity =>
            context._currentTenant.TenantId.HasValue && entity.TenantId == context._currentTenant.TenantId.Value);
    }
}
