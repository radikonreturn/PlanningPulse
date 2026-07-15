using PlanningPulse.Domain.Common;

namespace PlanningPulse.Domain.Identity;

public sealed class ApplicationUser : Entity
{
    public string Email { get; set; } = string.Empty;
    public string NormalizedEmail { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public ICollection<TenantUser> TenantUsers { get; set; } = new List<TenantUser>();
}
