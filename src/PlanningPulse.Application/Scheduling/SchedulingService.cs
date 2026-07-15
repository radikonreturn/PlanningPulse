namespace PlanningPulse.Application.Scheduling;

public sealed class SchedulingService : ISchedulingService
{
    public Task<ScheduleResult> ScheduleAsync(ScheduleRequest request, CancellationToken cancellationToken)
    {
        // Phase 3 starts with simple forward finite scheduling; the interface is isolated for later optimization.
        return Task.FromResult(new ScheduleResult(request.ProductionOrderId, Array.Empty<ScheduledOperation>()));
    }
}
