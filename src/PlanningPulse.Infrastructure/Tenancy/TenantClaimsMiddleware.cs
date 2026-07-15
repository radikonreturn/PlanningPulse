using Microsoft.AspNetCore.Http;
using System.Security.Claims;
using PlanningPulse.Application.Tenancy;

namespace PlanningPulse.Infrastructure.Tenancy;

public sealed class TenantClaimsMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, ITenantSetter tenantSetter)
    {
        var tenantClaim = context.User.FindFirstValue("tenant_id");
        if (Guid.TryParse(tenantClaim, out var tenantId))
        {
            tenantSetter.SetTenant(tenantId);
        }

        await next(context);
    }
}
