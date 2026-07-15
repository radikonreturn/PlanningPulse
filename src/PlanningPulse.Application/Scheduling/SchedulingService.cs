using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PlanningPulse.Application.Scheduling;

public sealed class SchedulingService : ISchedulingService
{
    private readonly ISchedulingDataProvider _dataProvider;

    public SchedulingService(ISchedulingDataProvider dataProvider)
    {
        _dataProvider = dataProvider;
    }

    public async Task<ScheduleResult> ScheduleAsync(ScheduleRequest request, CancellationToken cancellationToken)
    {
        var order = await _dataProvider.GetOrderDetailsAsync(request.ProductionOrderId, cancellationToken);
        if (order == null || order.RoutingId == null)
        {
            return new ScheduleResult(request.ProductionOrderId, Array.Empty<ScheduledOperation>());
        }

        var operations = await _dataProvider.GetRoutingOperationsAsync(order.RoutingId.Value, cancellationToken);
        if (operations.Count == 0)
        {
            return new ScheduleResult(request.ProductionOrderId, Array.Empty<ScheduledOperation>());
        }

        var existingAllocations = await _dataProvider.GetExistingAllocationsAsync(cancellationToken);
        var allocations = existingAllocations.ToDictionary(
            x => (x.WorkCenterId, x.Date),
            x => x.AllocatedHours);

        var scheduledOps = new List<ScheduledOperation>();
        DateOnly? orderStartDate = null;
        DateOnly? orderEndDate = null;

        if (request.Direction == SchedulingDirection.Forward)
        {
            // Forward scheduling
            var earliestStartDate = DateOnly.FromDateTime(DateTime.UtcNow);

            foreach (var op in operations.OrderBy(x => x.Sequence))
            {
                var remainingHours = op.SetupHours + (op.RunHoursPerUnit * order.Quantity);
                var currentDate = earliestStartDate.AddDays(op.QueueTimeDays);
                DateOnly? opStartDate = null;
                DateOnly? opEndDate = null;

                if (request.CapacityMode == CapacityMode.Finite && op.WorkCenterIsFiniteCapacity)
                {
                    while (remainingHours > 0)
                    {
                        var capacity = op.WorkCenterCapacityHoursPerDay;
                        allocations.TryGetValue((op.WorkCenterId, currentDate), out var allocated);
                        var available = Math.Max(0m, capacity - allocated);

                        if (available > 0)
                        {
                            var toAllocate = Math.Min(remainingHours, available);
                            allocations[(op.WorkCenterId, currentDate)] = allocated + toAllocate;
                            remainingHours -= toAllocate;

                            opStartDate ??= currentDate;
                            opEndDate = currentDate;
                        }

                        if (remainingHours > 0)
                        {
                            currentDate = currentDate.AddDays(1);
                        }
                    }
                }
                else
                {
                    // Infinite capacity
                    opStartDate = currentDate;
                    opEndDate = currentDate;
                }

                var startOffset = new DateTimeOffset(opStartDate!.Value.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
                var endOffset = new DateTimeOffset(opEndDate!.Value.ToDateTime(TimeOnly.MaxValue), TimeSpan.Zero);

                scheduledOps.Add(new ScheduledOperation(op.OperationId, op.WorkCenterId, startOffset, endOffset));

                orderStartDate ??= opStartDate;
                orderEndDate = opEndDate;

                // The next operation can start after this operation finishes plus its move time.
                earliestStartDate = opEndDate.Value.AddDays(op.MoveTimeDays + 1);
            }
        }
        else
        {
            // Backward scheduling
            var targetEndDate = order.DueDate;

            foreach (var op in operations.OrderByDescending(x => x.Sequence))
            {
                var remainingHours = op.SetupHours + (op.RunHoursPerUnit * order.Quantity);
                var currentDate = targetEndDate.AddDays(-op.MoveTimeDays);
                DateOnly? opStartDate = null;
                DateOnly? opEndDate = null;

                if (request.CapacityMode == CapacityMode.Finite && op.WorkCenterIsFiniteCapacity)
                {
                    while (remainingHours > 0)
                    {
                        var capacity = op.WorkCenterCapacityHoursPerDay;
                        allocations.TryGetValue((op.WorkCenterId, currentDate), out var allocated);
                        var available = Math.Max(0m, capacity - allocated);

                        if (available > 0)
                        {
                            var toAllocate = Math.Min(remainingHours, available);
                            allocations[(op.WorkCenterId, currentDate)] = allocated + toAllocate;
                            remainingHours -= toAllocate;

                            opEndDate ??= currentDate;
                            opStartDate = currentDate;
                        }

                        if (remainingHours > 0)
                        {
                            currentDate = currentDate.AddDays(-1);
                        }
                    }
                }
                else
                {
                    // Infinite capacity
                    opStartDate = currentDate;
                    opEndDate = currentDate;
                }

                var startOffset = new DateTimeOffset(opStartDate!.Value.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
                var endOffset = new DateTimeOffset(opEndDate!.Value.ToDateTime(TimeOnly.MaxValue), TimeSpan.Zero);

                scheduledOps.Add(new ScheduledOperation(op.OperationId, op.WorkCenterId, startOffset, endOffset));

                orderEndDate ??= opEndDate;
                orderStartDate = opStartDate;

                // The preceding operation must finish before this operation starts plus its queue time.
                targetEndDate = opStartDate.Value.AddDays(-op.QueueTimeDays - 1);
            }

            // Reverse the operations list so it is in sequence order (optional but cleaner)
            scheduledOps.Reverse();
        }

        await _dataProvider.SaveScheduleAsync(
            request.ProductionOrderId,
            orderStartDate,
            orderEndDate,
            scheduledOps,
            cancellationToken);

        return new ScheduleResult(request.ProductionOrderId, scheduledOps);
    }

    public async Task<IReadOnlyCollection<WorkCenterLoad>> GetWorkCenterLoadsAsync(int rollingDays, CancellationToken cancellationToken)
    {
        var workCenters = await _dataProvider.GetWorkCentersAsync(cancellationToken);
        var existingAllocations = await _dataProvider.GetExistingAllocationsAsync(cancellationToken);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var endDate = today.AddDays(rollingDays - 1);

        var allocationsByWorkCenter = existingAllocations
            .Where(x => x.Date >= today && x.Date <= endDate)
            .GroupBy(x => x.WorkCenterId)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.AllocatedHours));

        var loads = new List<WorkCenterLoad>();
        foreach (var wc in workCenters)
        {
            var capacityHours = wc.CapacityHoursPerDay * rollingDays;
            allocationsByWorkCenter.TryGetValue(wc.Id, out var allocatedHours);
            var loadPercent = capacityHours > 0 ? (allocatedHours / capacityHours) * 100m : 0m;

            loads.Add(new WorkCenterLoad(
                wc.Id,
                wc.Code,
                wc.Name,
                capacityHours,
                allocatedHours,
                Math.Round(loadPercent, 1)
            ));
        }

        return loads;
    }
}
