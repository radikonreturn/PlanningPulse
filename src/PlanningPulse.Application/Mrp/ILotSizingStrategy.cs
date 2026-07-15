namespace PlanningPulse.Application.Mrp;

public interface ILotSizingStrategy
{
    LotSizingMethod Method { get; }
    decimal CalculateOrderQuantity(decimal netQuantity, MrpItemSnapshot item);
    string GetReason(decimal netQuantity, decimal plannedQuantity, MrpItemSnapshot item);
}
