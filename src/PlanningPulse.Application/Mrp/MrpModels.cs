namespace PlanningPulse.Application.Mrp;

public enum LotSizingMethod
{
    LotForLot = 1,
    MinMax = 2,
    EconomicOrderQuantity = 3
}

public sealed record GrossRequirement(Guid ItemId, decimal Quantity, DateOnly RequiredDate);
public sealed record NetRequirement(Guid ItemId, decimal GrossQuantity, decimal AvailableQuantity, decimal NetQuantity, DateOnly RequiredDate);
public sealed record MrpRecommendation(Guid ItemId, decimal Quantity, DateOnly ReleaseDate, DateOnly DueDate, string RecommendationType, string Reason);

public sealed record MrpItemSnapshot(
    Guid ItemId,
    string ItemNumber,
    string Name,
    MrpItemType Type,
    decimal? MinimumOrderQuantity,
    decimal? MaximumInventoryQuantity,
    decimal? EconomicOrderQuantity);

public enum MrpItemType
{
    Purchased = 1,
    Manufactured = 2,
    Phantom = 3
}

public sealed record MrpBomLineSnapshot(
    Guid ParentItemId,
    Guid ComponentItemId,
    decimal QuantityPer,
    decimal ScrapFactor);

public sealed record MrpInventorySnapshot(
    Guid ItemId,
    decimal OnHandQuantity,
    decimal AllocatedQuantity,
    decimal OnOrderQuantity);

public sealed record MrpLeadTimeSnapshot(
    Guid ItemId,
    int ProcurementLeadTimeDays,
    int ManufacturingLeadTimeDays,
    int SafetyLeadTimeDays);

public sealed record MrpPlanningSnapshot(
    IReadOnlyDictionary<Guid, MrpItemSnapshot> Items,
    IReadOnlyDictionary<Guid, IReadOnlyCollection<MrpBomLineSnapshot>> BomLinesByParentItem,
    IReadOnlyDictionary<Guid, MrpInventorySnapshot> InventoryByItem,
    IReadOnlyDictionary<Guid, MrpLeadTimeSnapshot> LeadTimesByItem);
