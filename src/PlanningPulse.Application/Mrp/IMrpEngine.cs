namespace PlanningPulse.Application.Mrp;

public interface IMrpEngine
{
    Task<IReadOnlyCollection<MrpRecommendation>> PlanAsync(
        IReadOnlyCollection<GrossRequirement> grossRequirements,
        LotSizingMethod lotSizingMethod,
        CancellationToken cancellationToken);
}
