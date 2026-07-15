using PlanningPulse.Domain.Common;

namespace PlanningPulse.Domain.Routings;

public sealed class Operation : Entity, ITenantOwned
{
    public Guid TenantId { get; set; }
    public Guid RoutingId { get; set; }
    public Routing Routing { get; set; } = null!;
    public int Sequence { get; set; }
    public string Name { get; set; } = string.Empty;
    public Guid WorkCenterId { get; set; }
    public WorkCenter WorkCenter { get; set; } = null!;
    public decimal SetupHours { get; set; }
    public decimal RunHoursPerUnit { get; set; }
    public int QueueTimeDays { get; set; }
    public int MoveTimeDays { get; set; }
}
