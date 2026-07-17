using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PlanningPulse.Application.Mrp;
using PlanningPulse.Application.Tenancy;
using PlanningPulse.Domain.Identity;
using PlanningPulse.Domain.Production;
using PlanningPulse.Infrastructure.Mrp;
using PlanningPulse.Infrastructure.Persistence;
using PlanningPulse.Infrastructure.Tenancy;
using Xunit;
using Xunit.Abstractions;

namespace PlanningPulse.Tests.Mrp;

public sealed class MrpIntegrationTests
{
    private readonly ITestOutputHelper _output;

    public MrpIntegrationTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task TestRunMrpAgainstSeededDb()
    {
        // Path to the sqlite db file in the Web project
        var dbPath = @"c:\Users\abdul\OneDrive\Belgeler\gitler\PlanningPulse\src\PlanningPulse.Web\planningpulse.db";
        _output.WriteLine($"Database path: {Path.GetFullPath(dbPath)}");

        if (!File.Exists(dbPath))
        {
            _output.WriteLine("Database file does not exist, creating in-memory/temp database for test.");
        }

        var options = new DbContextOptionsBuilder<PlanningPulseDbContext>()
            .UseSqlite($"Data Source={dbPath}")
            .Options;

        var currentTenant = new CurrentTenant();
        var dbContext = new PlanningPulseDbContext(options, currentTenant);

        // Fetch tenant from db
        var tenant = await dbContext.Tenants.FirstOrDefaultAsync();
        Assert.NotNull(tenant);
        currentTenant.SetTenant(tenant.Id);

        _output.WriteLine($"Active Tenant: {tenant.Name} ({tenant.Id})");

        var items = await dbContext.Items.AsNoTracking().ToListAsync();
        _output.WriteLine($"Total items: {items.Count}");
        foreach (var item in items)
        {
            _output.WriteLine($" - {item.ItemNumber}: {item.Name} ({item.Type})");
        }

        var openOrders = await dbContext.ProductionOrders
            .AsNoTracking()
            .Where(x => x.Status != ProductionOrderStatus.Completed && x.Status != ProductionOrderStatus.Cancelled)
            .ToListAsync();
        _output.WriteLine($"Total open orders: {openOrders.Count}");

        var grossReqs = openOrders
            .Select(x => new GrossRequirement(x.ItemId, x.Quantity, x.DueDate))
            .ToArray();

        var provider = new EfMrpPlanningDataProvider(dbContext);
        var strategies = new ILotSizingStrategy[]
        {
            new LotForLotLotSizingStrategy(),
            new MinMaxLotSizingStrategy(),
            new EoqLotSizingStrategy()
        };
        var engine = new MrpEngine(provider, strategies);

        var result = await engine.PlanAsync(grossReqs, LotSizingMethod.LotForLot, CancellationToken.None);
        _output.WriteLine($"Total recommendations generated: {result.Count}");

        foreach (var rec in result)
        {
            var item = items.FirstOrDefault(i => i.Id == rec.ItemId);
            _output.WriteLine($"Recommendation: {rec.RecommendationType} | Qty: {rec.Quantity} | Item: {item?.ItemNumber} | Release: {rec.ReleaseDate} | Due: {rec.DueDate} | Reason: {rec.Reason}");
        }

        Assert.NotEmpty(result);
    }
}
