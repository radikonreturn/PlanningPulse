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
        services.Configure<JwtOptions>(options =>
        {
            configuration.GetSection(JwtOptions.SectionName).Bind(options);
            if (string.IsNullOrWhiteSpace(options.SigningKey))
            {
                options.SigningKey = Environment.GetEnvironmentVariable("JWT_SIGNING_KEY")
                    ?? Environment.GetEnvironmentVariable("Jwt__SigningKey")
                    ?? string.Empty;
            }
        });

        var provider = configuration.GetValue<string>("Database:Provider") ?? "Sqlite";
        var connectionString = configuration.GetConnectionString("PlanningPulse");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            connectionString = Environment.GetEnvironmentVariable("CONNECTION_STRING_PLANNING_PULSE")
                ?? Environment.GetEnvironmentVariable("ConnectionStrings__PlanningPulse");
        }

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("Connection string 'PlanningPulse' is not configured. Please set the environment variable 'ConnectionStrings__PlanningPulse' or 'CONNECTION_STRING_PLANNING_PULSE'.");
        }

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

        var jwtOptions = new JwtOptions();
        configuration.GetSection(JwtOptions.SectionName).Bind(jwtOptions);
        if (string.IsNullOrWhiteSpace(jwtOptions.SigningKey))
        {
            jwtOptions.SigningKey = Environment.GetEnvironmentVariable("JWT_SIGNING_KEY")
                ?? Environment.GetEnvironmentVariable("Jwt__SigningKey")
                ?? string.Empty;
        }

        if (string.IsNullOrWhiteSpace(jwtOptions.SigningKey))
        {
            throw new InvalidOperationException("JWT signing key is not configured. Please set the environment variable 'JWT_SIGNING_KEY' or 'Jwt__SigningKey' with a secret of at least 32 characters.");
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
