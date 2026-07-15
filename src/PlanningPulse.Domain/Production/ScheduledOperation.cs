using PlanningPulse.Domain.Common;
using PlanningPulse.Domain.Routings;

namespace PlanningPulse.Domain.Production;

public sealed class ScheduledOperation : Entity, ITenantOwned
{
    public Guid TenantId { get; set; }
    
    public Guid ProductionOrderId { get; set; }
    public ProductionOrder ProductionOrder { get; set; } = null!;
    
    public Guid OperationId { get; set; }
    public Operation Operation { get; set; } = null!;
    
    public Guid WorkCenterId { get; set; }
    public WorkCenter WorkCenter { get; set; } = null!;
    
    public int Sequence { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal SetupHours { get; set; }
    public decimal RunHours { get; set; }
    
    public DateOnly ScheduledStartDate { get; set; }
    public DateOnly ScheduledEndDate { get; set; }
}
