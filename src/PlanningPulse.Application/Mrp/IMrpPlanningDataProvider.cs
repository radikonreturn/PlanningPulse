namespace PlanningPulse.Application.Mrp;

public interface IMrpPlanningDataProvider
{
    Task<MrpPlanningSnapshot> GetPlanningSnapshotAsync(
        IReadOnlyCollection<Guid> rootItemIds,
        CancellationToken cancellationToken);
}
