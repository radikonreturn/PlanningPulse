using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using PlanningPulse.Application.Scheduling;
using PlanningPulse.Infrastructure.Persistence;

namespace PlanningPulse.Infrastructure.Scheduling;

public sealed class EfSchedulingDataProvider : ISchedulingDataProvider
{
    private readonly PlanningPulseDbContext _dbContext;

    public EfSchedulingDataProvider(PlanningPulseDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<SchedulingOrderDetails?> GetOrderDetailsAsync(Guid productionOrderId, CancellationToken cancellationToken)
    {
        var po = await _dbContext.ProductionOrders
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == productionOrderId, cancellationToken);

        if (po == null)
        {
            return null;
        }

        return new SchedulingOrderDetails(po.Id, po.Quantity, po.DueDate, po.RoutingId);
    }

    public async Task<IReadOnlyCollection<SchedulingOperationDetails>> GetRoutingOperationsAsync(Guid routingId, CancellationToken cancellationToken)
    {
        return await _dbContext.Operations
            .AsNoTracking()
            .Where(x => x.RoutingId == routingId)
            .Include(x => x.WorkCenter)
            .Select(x => new SchedulingOperationDetails(
                x.Id,
                x.Sequence,
                x.Name,
                x.WorkCenterId,
                x.SetupHours,
                x.RunHoursPerUnit,
                x.QueueTimeDays,
                x.MoveTimeDays,
                x.WorkCenter.CapacityHoursPerDay,
                x.WorkCenter.IsFiniteCapacity
            ))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<ExistingScheduleAllocation>> GetExistingAllocationsAsync(CancellationToken cancellationToken)
    {
        var scheduled = await _dbContext.ScheduledOperations
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var dailyAllocations = new Dictionary<(Guid WorkCenterId, DateOnly Date), decimal>();

        foreach (var op in scheduled)
        {
            var days = op.ScheduledEndDate.DayNumber - op.ScheduledStartDate.DayNumber + 1;
            var totalHours = op.SetupHours + op.RunHours;
            var hoursPerDay = days > 0 ? totalHours / days : totalHours;

            for (var date = op.ScheduledStartDate; date <= op.ScheduledEndDate; date = date.AddDays(1))
            {
                var key = (op.WorkCenterId, date);
                dailyAllocations[key] = dailyAllocations.GetValueOrDefault(key) + hoursPerDay;
            }
        }

        return dailyAllocations
            .Select(x => new ExistingScheduleAllocation(x.Key.WorkCenterId, x.Key.Date, x.Value))
            .ToArray();
    }
    public async Task<IReadOnlyCollection<SchedulingWorkCenterDetails>> GetWorkCentersAsync(CancellationToken cancellationToken)
    {
        return await _dbContext.WorkCenters
            .AsNoTracking()
            .Select(x => new SchedulingWorkCenterDetails(x.Id, x.Code, x.Name, x.CapacityHoursPerDay))
            .ToListAsync(cancellationToken);
    }
    public async Task SaveScheduleAsync(
        Guid productionOrderId,
        DateOnly? scheduledStartDate,
        DateOnly? scheduledEndDate,
        IReadOnlyCollection<ScheduledOperation> operations,
        CancellationToken cancellationToken)
    {
        var existing = await _dbContext.ScheduledOperations
            .Where(x => x.ProductionOrderId == productionOrderId)
            .ToListAsync(cancellationToken);

        _dbContext.ScheduledOperations.RemoveRange(existing);

        var po = await _dbContext.ProductionOrders
            .SingleOrDefaultAsync(x => x.Id == productionOrderId, cancellationToken);

        if (po != null)
        {
            po.ScheduledStartDate = scheduledStartDate;
            po.ScheduledEndDate = scheduledEndDate;
        }

        foreach (var op in operations)
        {
            var routingOp = await _dbContext.Operations
                .AsNoTracking()
                .SingleAsync(x => x.Id == op.OperationId, cancellationToken);

            var setup = routingOp.SetupHours;
            var run = routingOp.RunHoursPerUnit * (po?.Quantity ?? 0m);

            var domainOp = new PlanningPulse.Domain.Production.ScheduledOperation
            {
                ProductionOrderId = productionOrderId,
                OperationId = op.OperationId,
                WorkCenterId = op.WorkCenterId,
                Sequence = routingOp.Sequence,
                Name = routingOp.Name,
                SetupHours = setup,
                RunHours = run,
                ScheduledStartDate = DateOnly.FromDateTime(op.StartUtc.UtcDateTime),
                ScheduledEndDate = DateOnly.FromDateTime(op.EndUtc.UtcDateTime)
            };

            await _dbContext.ScheduledOperations.AddAsync(domainOp, cancellationToken);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
