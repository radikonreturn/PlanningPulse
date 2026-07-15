using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PlanningPulse.Application.Tenancy;
using PlanningPulse.Domain.Identity;
using PlanningPulse.Infrastructure;
using PlanningPulse.Infrastructure.Persistence;
using PlanningPulse.Infrastructure.Tenancy;
using PlanningPulse.Web;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddPlanningPulseInfrastructure(builder.Configuration);
builder.Services.AddSingleton<AppState>();
builder.Services.AddScoped<HttpClient>(sp =>
    new HttpClient { BaseAddress = new Uri("http://localhost:5000/") });

var app = builder.Build();

// Apply schema and seed default user
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<PlanningPulseDbContext>();
    await db.Database.EnsureCreatedAsync();
    var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<ApplicationUser>>();
    var tenantSetter = scope.ServiceProvider.GetRequiredService<ITenantSetter>();
    await DatabaseSeeder.SeedAsync(db, hasher, tenantSetter);
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

if (!app.Environment.IsDevelopment())
    app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseAuthentication();
app.UseMiddleware<TenantClaimsMiddleware>();
app.UseAuthorization();

app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "ok", app = "PlanningPulse" }));
app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

app.Run();

