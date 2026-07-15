using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using PlanningPulse.Infrastructure.Tenancy;

namespace PlanningPulse.Infrastructure.Persistence;

public sealed class PlanningPulseDesignTimeDbContextFactory : IDesignTimeDbContextFactory<PlanningPulseDbContext>
{
    public PlanningPulseDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<PlanningPulseDbContext>()
            .UseSqlite("Data Source=planningpulse.db")
            .Options;

        return new PlanningPulseDbContext(options, new CurrentTenant());
    }
}
