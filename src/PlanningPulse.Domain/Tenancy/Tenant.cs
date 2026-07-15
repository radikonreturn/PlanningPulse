using PlanningPulse.Domain.Common;

namespace PlanningPulse.Domain.Tenancy;

public sealed class Tenant : Entity
{
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}
