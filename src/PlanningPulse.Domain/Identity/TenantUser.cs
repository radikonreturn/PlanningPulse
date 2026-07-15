using PlanningPulse.Domain.Common;
using PlanningPulse.Domain.Tenancy;

namespace PlanningPulse.Domain.Identity;

public sealed class TenantUser : Entity, ITenantOwned
{
    public Guid TenantId { get; set; }
    public Tenant Tenant { get; set; } = null!;
    public Guid UserId { get; set; }
    public ApplicationUser User { get; set; } = null!;
    public TenantRole Role { get; set; } = TenantRole.Planner;
}
