using PlanningPulse.Domain.Common;
using PlanningPulse.Domain.Items;

namespace PlanningPulse.Domain.Inventory;

public sealed class InventoryLevel : Entity, ITenantOwned
{
    public Guid TenantId { get; set; }
    public Guid ItemId { get; set; }
    public Item Item { get; set; } = null!;
    public string LocationCode { get; set; } = string.Empty;
    public decimal OnHandQuantity { get; set; }
    public decimal AllocatedQuantity { get; set; }
    public decimal OnOrderQuantity { get; set; }
}
