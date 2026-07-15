using PlanningPulse.Domain.Common;

namespace PlanningPulse.Domain.Routings;

public sealed class WorkCenter : Entity, ITenantOwned
{
    public Guid TenantId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public decimal CapacityHoursPerDay { get; set; }
    public bool IsFiniteCapacity { get; set; } = true;
    public bool IsActive { get; set; } = true;
}
