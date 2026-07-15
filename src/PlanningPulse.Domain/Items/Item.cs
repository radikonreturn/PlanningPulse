using PlanningPulse.Domain.Common;

namespace PlanningPulse.Domain.Items;

public sealed class Item : Entity, ITenantOwned
{
    public Guid TenantId { get; set; }
    public string ItemNumber { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public ItemType Type { get; set; }
    public string UnitOfMeasure { get; set; } = "EA";
    public decimal? MinimumOrderQuantity { get; set; }
    public decimal? MaximumInventoryQuantity { get; set; }
    public decimal? EconomicOrderQuantity { get; set; }
    public bool IsActive { get; set; } = true;
}
