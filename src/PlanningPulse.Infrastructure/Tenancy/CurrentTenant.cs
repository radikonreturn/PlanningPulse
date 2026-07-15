using PlanningPulse.Application.Tenancy;

namespace PlanningPulse.Infrastructure.Tenancy;

public sealed class CurrentTenant : ICurrentTenant, ITenantSetter
{
    public Guid? TenantId { get; private set; }
    public bool IsSet => TenantId.HasValue;

    public void SetTenant(Guid tenantId)
    {
        TenantId = tenantId;
    }
}
