using PlanningPulse.Domain.Identity;

namespace PlanningPulse.Application.Auth;

public sealed record RegisterTenantRequest(string TenantName, string TenantSlug, string Email, string DisplayName, string Password);
public sealed record LoginRequest(string Email, string Password, string TenantSlug);
public sealed record AuthResponse(string AccessToken, DateTimeOffset ExpiresAtUtc, Guid TenantId, Guid UserId, TenantRole Role);
