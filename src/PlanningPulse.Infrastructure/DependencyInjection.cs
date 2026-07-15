using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using PlanningPulse.Application.Auth;
using PlanningPulse.Application.Mrp;
using PlanningPulse.Application.Scheduling;
using PlanningPulse.Application.Tenancy;
using PlanningPulse.Domain.Identity;
using PlanningPulse.Infrastructure.Auth;
using PlanningPulse.Infrastructure.Mrp;
using PlanningPulse.Infrastructure.Persistence;
using PlanningPulse.Infrastructure.Tenancy;
using PlanningPulse.Infrastructure.Scheduling;
using PlanningPulse.Application.Import;
using PlanningPulse.Infrastructure.Import;

namespace PlanningPulse.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddPlanningPulseInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));

        var provider = configuration.GetValue<string>("Database:Provider") ?? "Sqlite";
        var connectionString = configuration.GetConnectionString("PlanningPulse")
            ?? throw new InvalidOperationException("Connection string 'PlanningPulse' is not configured.");

        services.AddDbContext<PlanningPulseDbContext>(options =>
        {
            if (provider.Equals("Postgres", StringComparison.OrdinalIgnoreCase))
            {
                options.UseNpgsql(connectionString);
            }
            else
            {
                options.UseSqlite(connectionString);
            }
        });

        services.AddScoped<CurrentTenant>();
        services.AddScoped<ICurrentTenant>(sp => sp.GetRequiredService<CurrentTenant>());
        services.AddScoped<ITenantSetter>(sp => sp.GetRequiredService<CurrentTenant>());
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IJwtTokenService, JwtTokenService>();
        services.AddScoped<IPasswordHasher<ApplicationUser>, PasswordHasher<ApplicationUser>>();
        services.AddScoped<IMrpPlanningDataProvider, EfMrpPlanningDataProvider>();
        services.AddScoped<ILotSizingStrategy, LotForLotLotSizingStrategy>();
        services.AddScoped<ILotSizingStrategy, MinMaxLotSizingStrategy>();
        services.AddScoped<ILotSizingStrategy, EoqLotSizingStrategy>();
        services.AddScoped<IMrpEngine, MrpEngine>();
        services.AddScoped<ISchedulingDataProvider, EfSchedulingDataProvider>();
        services.AddScoped<ISchedulingService, SchedulingService>();
        services.AddScoped<IImportService, ImportService>();

        var jwtOptions = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();
        if (string.IsNullOrWhiteSpace(jwtOptions.SigningKey))
        {
            throw new InvalidOperationException("JWT signing key is not configured.");
        }

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateIssuerSigningKey = true,
                    ValidateLifetime = true,
                    ValidIssuer = jwtOptions.Issuer,
                    ValidAudience = jwtOptions.Audience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey))
                };
            });

        services.AddAuthorization();
        return services;
    }
}
