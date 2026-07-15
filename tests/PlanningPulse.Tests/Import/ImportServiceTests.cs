using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PlanningPulse.Application.Common.Validation;
using PlanningPulse.Application.Import;
using PlanningPulse.Application.Tenancy;
using PlanningPulse.Domain.Items;
using PlanningPulse.Domain.Tenancy;
using PlanningPulse.Infrastructure.Import;
using PlanningPulse.Infrastructure.Persistence;
using Xunit;

namespace PlanningPulse.Tests.Import;

public sealed class ImportServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly PlanningPulseDbContext _dbContext;
    private readonly StubCurrentTenant _currentTenant;

    public ImportServiceTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<PlanningPulseDbContext>()
            .UseSqlite(_connection)
            .Options;

        _currentTenant = new StubCurrentTenant { Id = Guid.NewGuid() };

        _dbContext = new PlanningPulseDbContext(options, _currentTenant);
        _dbContext.Database.EnsureCreated();

        // Seed Tenant to satisfy Foreign Key constraints
        var tenant = new Tenant { Id = _currentTenant.Id, Name = "Test Tenant", Slug = "test" };
        _dbContext.Tenants.Add(tenant);
        _dbContext.SaveChanges();
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        _connection.Close();
    }

    [Fact]
    public void EntityValidator_ItemValidation_DetectsErrors()
    {
        // Missing code
        var errors = EntityValidator.ValidateItem("", "Widget", "EA", "Manufactured", 0, 0);
        Assert.Contains(errors, x => x.Contains("Number is required"));

        // Negative values
        errors = EntityValidator.ValidateItem("FG-100", "Widget", "EA", "Manufactured", -5m, -2);
        Assert.Contains(errors, x => x.Contains("Safety Stock must be non-negative"));
        Assert.Contains(errors, x => x.Contains("Lead Time must be non-negative"));

        // Invalid type
        errors = EntityValidator.ValidateItem("FG-100", "Widget", "EA", "SuperGoods", 0, 0);
        Assert.Contains(errors, x => x.Contains("Invalid Item Type"));
    }

    [Fact]
    public async Task ImportItemsAsync_ValidCsv_InsertsRecordsInDatabase()
    {
        var csv = new StringBuilder();
        csv.AppendLine("ItemNumber,Name,Description,Type,UnitOfMeasure,SafetyStockQuantity,LeadTimeDays");
        csv.AppendLine("FG-500,Test Widget,Best widget,Manufactured,EA,10,5");
        csv.AppendLine("RM-500,Steel Part,Metal bar,Purchased,EA,100,2");

        var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv.ToString()));
        var service = new ImportService(_dbContext, _currentTenant);

        var result = await service.ImportItemsAsync(stream, "items.csv", CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(2, result.CreatedCount);
        Assert.Equal(0, result.UpdatedCount);
        Assert.Empty(result.Errors);

        // Verify items in Db
        var items = await _dbContext.Items.ToListAsync();
        Assert.Contains(items, x => x.ItemNumber == "FG-500" && x.Type == ItemType.Manufactured);
        Assert.Contains(items, x => x.ItemNumber == "RM-500" && x.Type == ItemType.Purchased);
    }

    [Fact]
    public async Task ImportItemsAsync_InvalidRow_RollsBackEverything()
    {
        var csv = new StringBuilder();
        csv.AppendLine("ItemNumber,Name,Description,Type,UnitOfMeasure,SafetyStockQuantity,LeadTimeDays");
        csv.AppendLine("FG-500,Test Widget,Best widget,Manufactured,EA,10,5");
        csv.AppendLine("RM-500,Steel Part,Metal bar,Purchased,EA,-100,2"); // Invalid safety stock

        var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv.ToString()));
        var service = new ImportService(_dbContext, _currentTenant);

        var result = await service.ImportItemsAsync(stream, "items.csv", CancellationToken.None);

        Assert.False(result.Success);
        Assert.NotEmpty(result.Errors);
        Assert.Equal(0, result.CreatedCount);

        // Verify database is completely empty (Atomic execution)
        var itemsCount = await _dbContext.Items.CountAsync();
        Assert.Equal(0, itemsCount);
    }

    [Fact]
    public async Task ImportBomsAsync_CircularDependency_DetectsCycleAndFails()
    {
        // 1. Seed two items
        var itemA = new Item { ItemNumber = "ITEM-A", Name = "A", Type = ItemType.Manufactured, UnitOfMeasure = "EA", TenantId = _currentTenant.Id };
        var itemB = new Item { ItemNumber = "ITEM-B", Name = "B", Type = ItemType.Manufactured, UnitOfMeasure = "EA", TenantId = _currentTenant.Id };
        await _dbContext.Items.AddRangeAsync(itemA, itemB);
        await _dbContext.SaveChangesAsync();

        // 2. Upload BOM that creates ITEM-A -> ITEM-B and ITEM-B -> ITEM-A cycle
        var csv = new StringBuilder();
        csv.AppendLine("ParentItemNumber,ComponentItemNumber,QuantityPer,ScrapFactor,Revision,EffectiveFrom");
        csv.AppendLine("ITEM-A,ITEM-B,1,0.0,Rev A,2026-07-15");
        csv.AppendLine("ITEM-B,ITEM-A,1,0.0,Rev A,2026-07-15");

        var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv.ToString()));
        var service = new ImportService(_dbContext, _currentTenant);

        var result = await service.ImportBomsAsync(stream, "boms.csv", CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains(result.Errors, x => x.ErrorMessage.Contains("Circular dependency detected"));
    }

    private sealed class StubCurrentTenant : ICurrentTenant
    {
        public Guid Id { get; set; }
        public Guid? TenantId => Id;
        public bool IsSet => true;
    }
}
