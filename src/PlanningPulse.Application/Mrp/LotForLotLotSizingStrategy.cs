namespace PlanningPulse.Application.Mrp;

public sealed class LotForLotLotSizingStrategy : ILotSizingStrategy
{
    public LotSizingMethod Method => LotSizingMethod.LotForLot;

    public decimal CalculateOrderQuantity(decimal netQuantity, MrpItemSnapshot item)
    {
        return netQuantity;
    }

    public string GetReason(decimal netQuantity, decimal plannedQuantity, MrpItemSnapshot item)
    {
        return $"Net requirement for {item.ItemNumber}. Lot-for-lot quantity equals net requirement.";
    }
}
