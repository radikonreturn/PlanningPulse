using PlanningPulse.Domain.Common;
using PlanningPulse.Domain.Items;
using PlanningPulse.Domain.Routings;

namespace PlanningPulse.Domain.Production;

public sealed class ProductionOrder : Entity, ITenantOwned
{
    public Guid TenantId { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public Guid ItemId { get; set; }
    public Item Item { get; set; } = null!;
    public Guid? RoutingId { get; set; }
    public Routing? Routing { get; set; }
    public decimal Quantity { get; set; }
    public ProductionOrderStatus Status { get; set; } = ProductionOrderStatus.Planned;
    public DateOnly DueDate { get; set; }
    public DateOnly? ScheduledStartDate { get; set; }
    public DateOnly? ScheduledEndDate { get; set; }
}
