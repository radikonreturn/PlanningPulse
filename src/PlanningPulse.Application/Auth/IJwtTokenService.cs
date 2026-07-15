using PlanningPulse.Domain.Identity;

namespace PlanningPulse.Application.Auth;

public interface IJwtTokenService
{
    AuthResponse CreateToken(Guid userId, Guid tenantId, string email, TenantRole role);
}
