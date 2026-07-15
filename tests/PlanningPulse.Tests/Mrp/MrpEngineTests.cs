using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using PlanningPulse.Application.Mrp;
using Xunit;

namespace PlanningPulse.Tests.Mrp;

public sealed class MrpEngineTests
{
    private static readonly ILotSizingStrategy[] Strategies = new ILotSizingStrategy[]
    {
        new LotForLotLotSizingStrategy(),
        new MinMaxLotSizingStrategy(),
        new EoqLotSizingStrategy()
    };

    [Fact]
    public async Task PlanAsync_LotForLot_NetsGrossRequirementAgainstAvailableInventory()
    {
        var itemId = Guid.NewGuid();
        var dueDate = new DateOnly(2026, 8, 10);
        var engine = new MrpEngine(
            new StubMrpPlanningDataProvider(new MrpPlanningSnapshot(
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
                })),
            Strategies);

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

        var engine = new MrpEngine(
            new StubMrpPlanningDataProvider(new MrpPlanningSnapshot(
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
                })),
            Strategies);

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
        var engine = new MrpEngine(
            new StubMrpPlanningDataProvider(new MrpPlanningSnapshot(
                Items: new Dictionary<Guid, MrpItemSnapshot>
                {
                    [itemId] = new(itemId, "RM-200", "Raw Material", MrpItemType.Purchased, null, null, 25m, 0m)
                },
                BomLinesByParentItem: new Dictionary<Guid, IReadOnlyCollection<MrpBomLineSnapshot>>(),
                InventoryByItem: new Dictionary<Guid, MrpInventorySnapshot>(),
                LeadTimesByItem: new Dictionary<Guid, MrpLeadTimeSnapshot>())),
            Strategies);

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
        var engine = new MrpEngine(
            new StubMrpPlanningDataProvider(new MrpPlanningSnapshot(
                Items: new Dictionary<Guid, MrpItemSnapshot>
                {
                    [itemId] = ManufacturedItem(itemId, "FG-404")
                },
                BomLinesByParentItem: new Dictionary<Guid, IReadOnlyCollection<MrpBomLineSnapshot>>(),
                InventoryByItem: new Dictionary<Guid, MrpInventorySnapshot>(),
                LeadTimesByItem: new Dictionary<Guid, MrpLeadTimeSnapshot>())),
            Strategies);

        var result = await engine.PlanAsync(
            [new GrossRequirement(itemId, 1m, new DateOnly(2026, 10, 1))],
            LotSizingMethod.LotForLot,
            CancellationToken.None);

        Assert.Contains(result, x => x.RecommendationType == "Exception" && x.Reason == "Manufactured item has no active BOM.");
    }

    [Fact]
    public async Task PlanAsync_SafetyStock_TriggersReplenishmentWhenInventoryFallsBelowSafetyStock()
    {
        var itemId = Guid.NewGuid();
        var dueDate = new DateOnly(2026, 8, 10);
        var engine = new MrpEngine(
            new StubMrpPlanningDataProvider(new MrpPlanningSnapshot(
                Items: new Dictionary<Guid, MrpItemSnapshot>
                {
                    [itemId] = new MrpItemSnapshot(itemId, "RM-SS", "Safety Item", MrpItemType.Purchased, null, null, null, 10m)
                },
                BomLinesByParentItem: new Dictionary<Guid, IReadOnlyCollection<MrpBomLineSnapshot>>(),
                InventoryByItem: new Dictionary<Guid, MrpInventorySnapshot>
                {
                    [itemId] = new(itemId, OnHandQuantity: 12m, AllocatedQuantity: 0m, OnOrderQuantity: 0m)
                },
                LeadTimesByItem: new Dictionary<Guid, MrpLeadTimeSnapshot>())),
            Strategies);

        var result = await engine.PlanAsync(
            [new GrossRequirement(itemId, 5m, dueDate)],
            LotSizingMethod.LotForLot,
            CancellationToken.None);

        // Shortage = (Gross 5 + Safety 10) - OnHand 12 = 3.
        var recommendation = Assert.Single(result);
        Assert.Equal("Purchase", recommendation.RecommendationType);
        Assert.Equal(3m, recommendation.Quantity);
        Assert.Equal(dueDate, recommendation.DueDate);
    }

    [Fact]
    public async Task PlanAsync_ParentInventory_PreventsComponentExplosion()
    {
        var parentId = Guid.NewGuid();
        var componentId = Guid.NewGuid();
        var dueDate = new DateOnly(2026, 8, 10);
        var engine = new MrpEngine(
            new StubMrpPlanningDataProvider(new MrpPlanningSnapshot(
                Items: new Dictionary<Guid, MrpItemSnapshot>
                {
                    [parentId] = ManufacturedItem(parentId, "PARENT"),
                    [componentId] = PurchasedItem(componentId, "COMP")
                },
                BomLinesByParentItem: new Dictionary<Guid, IReadOnlyCollection<MrpBomLineSnapshot>>
                {
                    [parentId] = [new MrpBomLineSnapshot(parentId, componentId, QuantityPer: 2m, ScrapFactor: 0m)]
                },
                InventoryByItem: new Dictionary<Guid, MrpInventorySnapshot>
                {
                    [parentId] = new(parentId, OnHandQuantity: 10m, AllocatedQuantity: 0m, OnOrderQuantity: 0m),
                    [componentId] = new(componentId, OnHandQuantity: 0m, AllocatedQuantity: 0m, OnOrderQuantity: 0m)
                },
                LeadTimesByItem: new Dictionary<Guid, MrpLeadTimeSnapshot>())),
            Strategies);

        // We demand 8 parent items. Since parent has 10 on-hand, parent net requirements is 0.
        // Therefore, parent planned order release is 0, which explodes to 0 components.
        // No recommendations should be created at all!
        var result = await engine.PlanAsync(
            [new GrossRequirement(parentId, 8m, dueDate)],
            LotSizingMethod.LotForLot,
            CancellationToken.None);

        Assert.Empty(result);
    }

    [Fact]
    public async Task PlanAsync_PhantomItem_BypassesInventoryAndExplodesComponents()
    {
        var parentId = Guid.NewGuid();
        var phantomId = Guid.NewGuid();
        var componentId = Guid.NewGuid();
        var dueDate = new DateOnly(2026, 8, 10);
        var engine = new MrpEngine(
            new StubMrpPlanningDataProvider(new MrpPlanningSnapshot(
                Items: new Dictionary<Guid, MrpItemSnapshot>
                {
                    [parentId] = ManufacturedItem(parentId, "PARENT"),
                    [phantomId] = new MrpItemSnapshot(phantomId, "PHANTOM", "Phantom", MrpItemType.Phantom, null, null, null, 0m),
                    [componentId] = PurchasedItem(componentId, "COMP")
                },
                BomLinesByParentItem: new Dictionary<Guid, IReadOnlyCollection<MrpBomLineSnapshot>>
                {
                    [parentId] = [new MrpBomLineSnapshot(parentId, phantomId, QuantityPer: 1m, ScrapFactor: 0m)],
                    [phantomId] = [new MrpBomLineSnapshot(phantomId, componentId, QuantityPer: 5m, ScrapFactor: 0.1m)]
                },
                InventoryByItem: new Dictionary<Guid, MrpInventorySnapshot>
                {
                    // Phantom has inventory, but MRP should bypass/ignore it
                    [phantomId] = new(phantomId, OnHandQuantity: 10m, AllocatedQuantity: 0m, OnOrderQuantity: 0m)
                },
                LeadTimesByItem: new Dictionary<Guid, MrpLeadTimeSnapshot>())),
            Strategies);

        var result = await engine.PlanAsync(
            [new GrossRequirement(parentId, 2m, dueDate)],
            LotSizingMethod.LotForLot,
            CancellationToken.None);

        // 2 Parent -> triggers production recommendation for parent (qty 2)
        // 2 Parent planned release -> explodes to 2 Phantom (transient, no recommendation)
        // 2 Phantom demand -> bypasses phantom inventory, explodes to 2 * 5 * 1.1 = 11 Component
        // Component has 0 inventory -> triggers purchase recommendation for component (qty 11)
        Assert.Equal(2, result.Count);
        
        Assert.Contains(result, r => r.ItemId == parentId && r.Quantity == 2m && r.RecommendationType == "Production");
        Assert.Contains(result, r => r.ItemId == componentId && r.Quantity == 11m && r.RecommendationType == "Purchase");
    }

    private static MrpItemSnapshot PurchasedItem(Guid itemId, string itemNumber)
    {
        return new MrpItemSnapshot(itemId, itemNumber, itemNumber, MrpItemType.Purchased, null, null, null, 0m);
    }

    private static MrpItemSnapshot ManufacturedItem(Guid itemId, string itemNumber)
    {
        return new MrpItemSnapshot(itemId, itemNumber, itemNumber, MrpItemType.Manufactured, null, null, null, 0m);
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
