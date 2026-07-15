using PlanningPulse.Domain.Common;
using PlanningPulse.Domain.Items;

namespace PlanningPulse.Domain.Routings;

public sealed class Routing : Entity, ITenantOwned
{
    public Guid TenantId { get; set; }
    public Guid ItemId { get; set; }
    public Item Item { get; set; } = null!;
    public string Revision { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public ICollection<Operation> Operations { get; set; } = new List<Operation>();
}
