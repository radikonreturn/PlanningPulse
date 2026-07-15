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
