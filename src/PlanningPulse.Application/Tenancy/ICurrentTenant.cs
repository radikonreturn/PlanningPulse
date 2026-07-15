namespace PlanningPulse.Application.Tenancy;

public interface ICurrentTenant
{
    Guid? TenantId { get; }
    bool IsSet { get; }
}
