namespace PlanningPulse.Application.Mrp;

public sealed class EoqLotSizingStrategy : ILotSizingStrategy
{
    public LotSizingMethod Method => LotSizingMethod.EconomicOrderQuantity;

    public decimal CalculateOrderQuantity(decimal netQuantity, MrpItemSnapshot item)
    {
        if (!item.EconomicOrderQuantity.HasValue || item.EconomicOrderQuantity.Value <= 0)
        {
            return netQuantity;
        }

        var eoq = item.EconomicOrderQuantity.Value;
        return Math.Ceiling(netQuantity / eoq) * eoq;
    }

    public string GetReason(decimal netQuantity, decimal plannedQuantity, MrpItemSnapshot item)
    {
        var baseReason = $"Net requirement for {item.ItemNumber}.";
        if (plannedQuantity != netQuantity)
        {
            return $"{baseReason} Quantity rounded to EOQ multiple.";
        }
        return $"{baseReason} Lot-for-lot quantity equals net requirement.";
    }
}
