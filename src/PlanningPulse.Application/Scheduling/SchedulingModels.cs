namespace PlanningPulse.Application.Scheduling;

public enum CapacityMode
{
    Infinite = 1,
    Finite = 2
}

public enum SchedulingDirection
{
    Forward = 1,
    Backward = 2
}

public sealed record ScheduleRequest(Guid ProductionOrderId, CapacityMode CapacityMode, SchedulingDirection Direction);
public sealed record ScheduledOperation(Guid OperationId, Guid WorkCenterId, DateTimeOffset StartUtc, DateTimeOffset EndUtc);
public sealed record ScheduleResult(Guid ProductionOrderId, IReadOnlyCollection<ScheduledOperation> Operations);
public sealed record WorkCenterLoad(Guid WorkCenterId, string WorkCenterCode, string WorkCenterName, decimal CapacityHours, decimal AllocatedHours, decimal LoadPercentage);
