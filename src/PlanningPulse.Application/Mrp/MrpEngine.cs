namespace PlanningPulse.Application.Mrp;

public sealed class MrpEngine : IMrpEngine
{
    public Task<IReadOnlyCollection<MrpRecommendation>> PlanAsync(
        IReadOnlyCollection<GrossRequirement> grossRequirements,
        LotSizingMethod lotSizingMethod,
        CancellationToken cancellationToken)
    {
        // Phase 2 will replace this scaffold with BOM explosion, netting, lead-time offsetting, and lot sizing policies.
        IReadOnlyCollection<MrpRecommendation> recommendations = Array.Empty<MrpRecommendation>();
        return Task.FromResult(recommendations);
    }
}
