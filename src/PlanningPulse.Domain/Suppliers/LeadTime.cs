using PlanningPulse.Domain.Common;
using PlanningPulse.Domain.Items;

namespace PlanningPulse.Domain.Suppliers;

public sealed class LeadTime : Entity, ITenantOwned
{
    public Guid TenantId { get; set; }
    public Guid ItemId { get; set; }
    public Item Item { get; set; } = null!;
    public Guid? SupplierId { get; set; }
    public Supplier? Supplier { get; set; }
    public int ProcurementLeadTimeDays { get; set; }
    public int ManufacturingLeadTimeDays { get; set; }
    public int SafetyLeadTimeDays { get; set; }
}
