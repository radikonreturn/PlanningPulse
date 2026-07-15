namespace PlanningPulse.Application.Tenancy;

public interface ITenantSetter
{
    void SetTenant(Guid tenantId);
}
