using PlanningPulse.Domain.Common;
using PlanningPulse.Domain.Items;

namespace PlanningPulse.Domain.Boms;

public sealed class Bom : Entity, ITenantOwned
{
    public Guid TenantId { get; set; }
    public Guid ParentItemId { get; set; }
    public Item ParentItem { get; set; } = null!;
    public string Revision { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateOnly EffectiveFrom { get; set; }
    public DateOnly? EffectiveTo { get; set; }
    public ICollection<BomLine> Lines { get; set; } = new List<BomLine>();
}
