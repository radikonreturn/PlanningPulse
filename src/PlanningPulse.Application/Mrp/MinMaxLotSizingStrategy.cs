namespace PlanningPulse.Application.Mrp;

public sealed class MinMaxLotSizingStrategy : ILotSizingStrategy
{
    public LotSizingMethod Method => LotSizingMethod.MinMax;

    public decimal CalculateOrderQuantity(decimal netQuantity, MrpItemSnapshot item)
    {
        var plannedQuantity = netQuantity;

        if (item.MaximumInventoryQuantity.HasValue && item.MaximumInventoryQuantity.Value > plannedQuantity)
        {
            plannedQuantity = item.MaximumInventoryQuantity.Value;
        }

        if (item.MinimumOrderQuantity.HasValue && plannedQuantity < item.MinimumOrderQuantity.Value)
        {
            plannedQuantity = item.MinimumOrderQuantity.Value;
        }

        return plannedQuantity;
    }

    public string GetReason(decimal netQuantity, decimal plannedQuantity, MrpItemSnapshot item)
    {
        var baseReason = $"Net requirement for {item.ItemNumber}.";
        if (plannedQuantity != netQuantity)
        {
            return $"{baseReason} Quantity adjusted by min/max planning policy.";
        }
        return $"{baseReason} Lot-for-lot quantity equals net requirement.";
    }
}
