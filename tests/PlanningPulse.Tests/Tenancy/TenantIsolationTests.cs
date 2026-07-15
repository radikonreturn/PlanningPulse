using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PlanningPulse.Domain.Items;
using PlanningPulse.Domain.Tenancy;
using PlanningPulse.Infrastructure.Persistence;
using PlanningPulse.Infrastructure.Tenancy;

namespace PlanningPulse.Tests.Tenancy;

public sealed class TenantIsolationTests
{
    [Fact]
    public async Task TenantOwnedQueries_ReturnOnlyCurrentTenantRows()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        var tenantContext = new CurrentTenant();
        tenantContext.SetTenant(tenantA);

        var options = new DbContextOptionsBuilder<PlanningPulseDbContext>()
            .UseSqlite(connection)
            .Options;

        await using (var setupContext = new PlanningPulseDbContext(options, tenantContext))
        {
            await setupContext.Database.EnsureCreatedAsync();
            setupContext.Tenants.AddRange(
                new Tenant { Id = tenantA, Name = "Tenant A", Slug = "tenant-a" },
                new Tenant { Id = tenantB, Name = "Tenant B", Slug = "tenant-b" });
            setupContext.Items.AddRange(
                new Item { TenantId = tenantA, ItemNumber = "A-100", Name = "Tenant A Item", Type = ItemType.Manufactured },
                new Item { TenantId = tenantB, ItemNumber = "B-100", Name = "Tenant B Item", Type = ItemType.Manufactured });
            await setupContext.SaveChangesAsync();
        }

        var tenantAContext = new CurrentTenant();
        tenantAContext.SetTenant(tenantA);

        await using var queryContext = new PlanningPulseDbContext(options, tenantAContext);
        var itemNumbers = await queryContext.Items.Select(x => x.ItemNumber).ToListAsync();

        Assert.Equal(["A-100"], itemNumbers);
    }
}
