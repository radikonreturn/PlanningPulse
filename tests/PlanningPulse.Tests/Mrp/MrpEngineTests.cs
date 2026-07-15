using PlanningPulse.Application.Mrp;

namespace PlanningPulse.Tests.Mrp;

public sealed class MrpEngineTests
{
    [Fact]
    public async Task PlanAsync_LotForLot_NetsGrossRequirementAgainstAvailableInventory()
    {
        var itemId = Guid.NewGuid();
        var dueDate = new DateOnly(2026, 8, 10);
        var engine = new MrpEngine(new StubMrpPlanningDataProvider(new MrpPlanningSnapshot(
            Items: new Dictionary<Guid, MrpItemSnapshot>
            {
                [itemId] = PurchasedItem(itemId, "RM-100")
            },
            BomLinesByParentItem: new Dictionary<Guid, IReadOnlyCollection<MrpBomLineSnapshot>>(),
            InventoryByItem: new Dictionary<Guid, MrpInventorySnapshot>
            {
                [itemId] = new(itemId, OnHandQuantity: 4m, AllocatedQuantity: 1m, OnOrderQuantity: 2m)
            },
            LeadTimesByItem: new Dictionary<Guid, MrpLeadTimeSnapshot>
            {
                [itemId] = new(itemId, ProcurementLeadTimeDays: 3, ManufacturingLeadTimeDays: 0, SafetyLeadTimeDays: 1)
            })));

        var result = await engine.PlanAsync(
            [new GrossRequirement(itemId, 10m, dueDate)],
            LotSizingMethod.LotForLot,
            CancellationToken.None);

        var recommendation = Assert.Single(result);
        Assert.Equal("Purchase", recommendation.RecommendationType);
        Assert.Equal(5m, recommendation.Quantity);
        Assert.Equal(new DateOnly(2026, 8, 6), recommendation.ReleaseDate);
        Assert.Equal(dueDate, recommendation.DueDate);
    }

    [Fact]
    public async Task PlanAsync_MultiLevelBomExplosion_CreatesComponentRecommendationsAtParentReleaseDates()
    {
        var finishedGoodId = Guid.NewGuid();
        var subAssemblyId = Guid.NewGuid();
        var rawMaterialId = Guid.NewGuid();
        var dueDate = new DateOnly(2026, 9, 30);

        var engine = new MrpEngine(new StubMrpPlanningDataProvider(new MrpPlanningSnapshot(
            Items: new Dictionary<Guid, MrpItemSnapshot>
            {
                [finishedGoodId] = ManufacturedItem(finishedGoodId, "FG-100"),
                [subAssemblyId] = ManufacturedItem(subAssemblyId, "SA-100"),
                [rawMaterialId] = PurchasedItem(rawMaterialId, "RM-100")
            },
            BomLinesByParentItem: new Dictionary<Guid, IReadOnlyCollection<MrpBomLineSnapshot>>
            {
                [finishedGoodId] = [new MrpBomLineSnapshot(finishedGoodId, subAssemblyId, QuantityPer: 2m, ScrapFactor: 0m)],
                [subAssemblyId] = [new MrpBomLineSnapshot(subAssemblyId, rawMaterialId, QuantityPer: 3m, ScrapFactor: 0.1m)]
            },
            InventoryByItem: new Dictionary<Guid, MrpInventorySnapshot>(),
            LeadTimesByItem: new Dictionary<Guid, MrpLeadTimeSnapshot>
            {
                [finishedGoodId] = new(finishedGoodId, ProcurementLeadTimeDays: 0, ManufacturingLeadTimeDays: 5, SafetyLeadTimeDays: 1),
                [subAssemblyId] = new(subAssemblyId, ProcurementLeadTimeDays: 0, ManufacturingLeadTimeDays: 2, SafetyLeadTimeDays: 0),
                [rawMaterialId] = new(rawMaterialId, ProcurementLeadTimeDays: 4, ManufacturingLeadTimeDays: 0, SafetyLeadTimeDays: 0)
            })));

        var result = await engine.PlanAsync(
            [new GrossRequirement(finishedGoodId, 10m, dueDate)],
            LotSizingMethod.LotForLot,
            CancellationToken.None);

        Assert.Contains(result, x =>
            x.ItemId == finishedGoodId &&
            x.RecommendationType == "Production" &&
            x.Quantity == 10m &&
            x.ReleaseDate == new DateOnly(2026, 9, 24));

        Assert.Contains(result, x =>
            x.ItemId == subAssemblyId &&
            x.RecommendationType == "Production" &&
            x.Quantity == 20m &&
            x.DueDate == new DateOnly(2026, 9, 24) &&
            x.ReleaseDate == new DateOnly(2026, 9, 22));

        Assert.Contains(result, x =>
            x.ItemId == rawMaterialId &&
            x.RecommendationType == "Purchase" &&
            x.Quantity == 66m &&
            x.DueDate == new DateOnly(2026, 9, 22) &&
            x.ReleaseDate == new DateOnly(2026, 9, 18));
    }

    [Fact]
    public async Task PlanAsync_Eoq_RoundsNetRequirementToEoqMultiple()
    {
        var itemId = Guid.NewGuid();
        var engine = new MrpEngine(new StubMrpPlanningDataProvider(new MrpPlanningSnapshot(
            Items: new Dictionary<Guid, MrpItemSnapshot>
            {
                [itemId] = new(itemId, "RM-200", "Raw Material", MrpItemType.Purchased, null, null, 25m)
            },
            BomLinesByParentItem: new Dictionary<Guid, IReadOnlyCollection<MrpBomLineSnapshot>>(),
            InventoryByItem: new Dictionary<Guid, MrpInventorySnapshot>(),
            LeadTimesByItem: new Dictionary<Guid, MrpLeadTimeSnapshot>())));

        var result = await engine.PlanAsync(
            [new GrossRequirement(itemId, 60m, new DateOnly(2026, 10, 1))],
            LotSizingMethod.EconomicOrderQuantity,
            CancellationToken.None);

        Assert.Equal(75m, Assert.Single(result).Quantity);
    }

    [Fact]
    public async Task PlanAsync_ManufacturedItemWithoutBom_ReturnsExceptionRecommendation()
    {
        var itemId = Guid.NewGuid();
        var engine = new MrpEngine(new StubMrpPlanningDataProvider(new MrpPlanningSnapshot(
            Items: new Dictionary<Guid, MrpItemSnapshot>
            {
                [itemId] = ManufacturedItem(itemId, "FG-404")
            },
            BomLinesByParentItem: new Dictionary<Guid, IReadOnlyCollection<MrpBomLineSnapshot>>(),
            InventoryByItem: new Dictionary<Guid, MrpInventorySnapshot>(),
            LeadTimesByItem: new Dictionary<Guid, MrpLeadTimeSnapshot>())));

        var result = await engine.PlanAsync(
            [new GrossRequirement(itemId, 1m, new DateOnly(2026, 10, 1))],
            LotSizingMethod.LotForLot,
            CancellationToken.None);

        Assert.Contains(result, x => x.RecommendationType == "Exception" && x.Reason == "Manufactured item has no active BOM.");
    }

    private static MrpItemSnapshot PurchasedItem(Guid itemId, string itemNumber)
    {
        return new MrpItemSnapshot(itemId, itemNumber, itemNumber, MrpItemType.Purchased, null, null, null);
    }

    private static MrpItemSnapshot ManufacturedItem(Guid itemId, string itemNumber)
    {
        return new MrpItemSnapshot(itemId, itemNumber, itemNumber, MrpItemType.Manufactured, null, null, null);
    }

    private sealed class StubMrpPlanningDataProvider(MrpPlanningSnapshot snapshot) : IMrpPlanningDataProvider
    {
        public Task<MrpPlanningSnapshot> GetPlanningSnapshotAsync(
            IReadOnlyCollection<Guid> rootItemIds,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(snapshot);
        }
    }
}
