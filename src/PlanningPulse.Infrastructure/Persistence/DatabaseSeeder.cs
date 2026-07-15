using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PlanningPulse.Application.Tenancy;
using PlanningPulse.Domain.Identity;
using PlanningPulse.Domain.Tenancy;

namespace PlanningPulse.Infrastructure.Persistence;

public static class DatabaseSeeder
{
    public static async Task SeedAsync(PlanningPulseDbContext dbContext, IPasswordHasher<ApplicationUser> passwordHasher, ITenantSetter tenantSetter)
    {
        // Only seed if no tenants exist yet
        if (await dbContext.Tenants.AnyAsync())
            return;

        var tenant = new Tenant { Name = "Demo Workspace", Slug = "demo" };
        var user = new ApplicationUser
        {
            Email = "admin@demo.com",
            NormalizedEmail = "ADMIN@DEMO.COM",
            DisplayName = "Admin"
        };
        user.PasswordHash = passwordHasher.HashPassword(user, "Admin123!");

        await dbContext.Tenants.AddAsync(tenant);
        await dbContext.Users.AddAsync(user);
        await dbContext.SaveChangesAsync();

        tenantSetter.SetTenant(tenant.Id);
        await dbContext.TenantUsers.AddAsync(new TenantUser
        {
            TenantId = tenant.Id,
            UserId = user.Id,
            Role = TenantRole.Admin
        });
        await dbContext.SaveChangesAsync();
    }
}
