using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace PlanningPulse.Application.Scheduling;

public sealed record SchedulingOrderDetails(
    Guid ProductionOrderId,
    decimal Quantity,
    DateOnly DueDate,
    Guid? RoutingId);

public sealed record SchedulingOperationDetails(
    Guid OperationId,
    int Sequence,
    string Name,
    Guid WorkCenterId,
    decimal SetupHours,
    decimal RunHoursPerUnit,
    int QueueTimeDays,
    int MoveTimeDays,
    decimal WorkCenterCapacityHoursPerDay,
    bool WorkCenterIsFiniteCapacity);

public sealed record ExistingScheduleAllocation(
    Guid WorkCenterId,
    DateOnly Date,
    decimal AllocatedHours);

public sealed record SchedulingWorkCenterDetails(Guid Id, string Code, string Name, decimal CapacityHoursPerDay);

public interface ISchedulingDataProvider
{
    Task<SchedulingOrderDetails?> GetOrderDetailsAsync(Guid productionOrderId, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<SchedulingOperationDetails>> GetRoutingOperationsAsync(Guid routingId, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<ExistingScheduleAllocation>> GetExistingAllocationsAsync(CancellationToken cancellationToken);
    Task<IReadOnlyCollection<SchedulingWorkCenterDetails>> GetWorkCentersAsync(CancellationToken cancellationToken);
    Task SaveScheduleAsync(
        Guid productionOrderId,
        DateOnly? scheduledStartDate,
        DateOnly? scheduledEndDate,
        IReadOnlyCollection<ScheduledOperation> operations,
        CancellationToken cancellationToken);
}
