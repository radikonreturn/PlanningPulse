using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PlanningPulse.Application.Auth;
using PlanningPulse.Application.Tenancy;
using PlanningPulse.Domain.Identity;
using PlanningPulse.Domain.Tenancy;
using PlanningPulse.Infrastructure.Persistence;

namespace PlanningPulse.Infrastructure.Auth;

public sealed class AuthService(
    PlanningPulseDbContext dbContext,
    IJwtTokenService jwtTokenService,
    IPasswordHasher<ApplicationUser> passwordHasher,
    ITenantSetter tenantSetter) : IAuthService
{
    public async Task<AuthResponse> RegisterTenantAsync(RegisterTenantRequest request, CancellationToken cancellationToken)
    {
        var normalizedEmail = NormalizeEmail(request.Email);
        var normalizedSlug = NormalizeSlug(request.TenantSlug);

        var slugExists = await dbContext.Tenants.AnyAsync(x => x.Slug == normalizedSlug, cancellationToken);
        if (slugExists)
        {
            throw new InvalidOperationException("Tenant slug is already registered.");
        }

        var emailExists = await dbContext.Users.AnyAsync(x => x.NormalizedEmail == normalizedEmail, cancellationToken);
        if (emailExists)
        {
            throw new InvalidOperationException("Email is already registered.");
        }

        var tenant = new Tenant { Name = request.TenantName.Trim(), Slug = normalizedSlug };
        var user = new ApplicationUser
        {
            Email = request.Email.Trim(),
            NormalizedEmail = normalizedEmail,
            DisplayName = request.DisplayName.Trim()
        };
        user.PasswordHash = passwordHasher.HashPassword(user, request.Password);

        await dbContext.Tenants.AddAsync(tenant, cancellationToken);
        await dbContext.Users.AddAsync(user, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        tenantSetter.SetTenant(tenant.Id);
        await dbContext.TenantUsers.AddAsync(new TenantUser
        {
            TenantId = tenant.Id,
            UserId = user.Id,
            Role = TenantRole.Admin
        }, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        return jwtTokenService.CreateToken(user.Id, tenant.Id, user.Email, TenantRole.Admin);
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        var normalizedEmail = NormalizeEmail(request.Email);
        var normalizedSlug = NormalizeSlug(request.TenantSlug);

        var tenant = await dbContext.Tenants.SingleOrDefaultAsync(x => x.Slug == normalizedSlug && x.IsActive, cancellationToken)
            ?? throw new UnauthorizedAccessException("Invalid login.");

        tenantSetter.SetTenant(tenant.Id);

        var user = await dbContext.Users.SingleOrDefaultAsync(x => x.NormalizedEmail == normalizedEmail && x.IsActive, cancellationToken)
            ?? throw new UnauthorizedAccessException("Invalid login.");

        var verification = passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
        if (verification == PasswordVerificationResult.Failed)
        {
            throw new UnauthorizedAccessException("Invalid login.");
        }

        var membership = await dbContext.TenantUsers.SingleOrDefaultAsync(x => x.UserId == user.Id && x.TenantId == tenant.Id, cancellationToken)
            ?? throw new UnauthorizedAccessException("Invalid login.");

        return jwtTokenService.CreateToken(user.Id, tenant.Id, user.Email, membership.Role);
    }

    private static string NormalizeEmail(string email) => email.Trim().ToUpperInvariant();
    private static string NormalizeSlug(string slug) => slug.Trim().ToLowerInvariant();
}
