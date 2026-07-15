using PlanningPulse.Domain.Common;
using PlanningPulse.Domain.Items;

namespace PlanningPulse.Domain.Boms;

public sealed class BomLine : Entity, ITenantOwned
{
    public Guid TenantId { get; set; }
    public Guid BomId { get; set; }
    public Bom Bom { get; set; } = null!;
    public Guid ComponentItemId { get; set; }
    public Item ComponentItem { get; set; } = null!;
    public decimal QuantityPer { get; set; }
    public decimal ScrapFactor { get; set; }
}
