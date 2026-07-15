using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using PlanningPulse.Application.Scheduling;
using Xunit;

namespace PlanningPulse.Tests.Scheduling;

public sealed class SchedulingServiceTests
{
    [Fact]
    public async Task ScheduleAsync_ForwardFiniteCapacity_DistributesHoursAcrossDays()
    {
        var orderId = Guid.NewGuid();
        var routingId = Guid.NewGuid();
        var wcId = Guid.NewGuid();
        var opId = Guid.NewGuid();

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var dataProvider = new StubSchedulingDataProvider
        {
            Order = new SchedulingOrderDetails(orderId, Quantity: 10m, DueDate: today.AddDays(10), RoutingId: routingId),
            Operations = new List<SchedulingOperationDetails>
            {
                new SchedulingOperationDetails(
                    opId,
                    Sequence: 10,
                    Name: "Assembly",
                    wcId,
                    SetupHours: 2m,
                    RunHoursPerUnit: 1m, // Total hours = 2 + 10*1 = 12
                    QueueTimeDays: 0,
                    MoveTimeDays: 0,
                    WorkCenterCapacityHoursPerDay: 8m,
                    WorkCenterIsFiniteCapacity: true)
            }
        };

        var service = new SchedulingService(dataProvider);

        var result = await service.ScheduleAsync(
            new ScheduleRequest(orderId, CapacityMode.Finite, SchedulingDirection.Forward),
            CancellationToken.None);

        Assert.Equal(orderId, result.ProductionOrderId);
        var scheduledOp = Assert.Single(result.Operations);
        Assert.Equal(opId, scheduledOp.OperationId);

        // Forward finite scheduling:
        // Day 1: Setup + Run = 8 hours (WC capacity maxed). StartDate = today.
        // Day 2: Setup + Run = 4 hours. EndDate = today + 1 day.
        var expectedStart = new DateTimeOffset(today.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        var expectedEnd = new DateTimeOffset(today.AddDays(1).ToDateTime(TimeOnly.MaxValue), TimeSpan.Zero);

        Assert.Equal(expectedStart, scheduledOp.StartUtc);
        Assert.Equal(expectedEnd, scheduledOp.EndUtc);

        // Verify it saved back the dates
        Assert.Equal(today, dataProvider.SavedStartDate);
        Assert.Equal(today.AddDays(1), dataProvider.SavedEndDate);
    }

    [Fact]
    public async Task ScheduleAsync_ForwardInfiniteCapacity_SchedulesEverythingOnSingleDay()
    {
        var orderId = Guid.NewGuid();
        var routingId = Guid.NewGuid();
        var wcId = Guid.NewGuid();
        var opId = Guid.NewGuid();

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var dataProvider = new StubSchedulingDataProvider
        {
            Order = new SchedulingOrderDetails(orderId, Quantity: 10m, DueDate: today.AddDays(10), RoutingId: routingId),
            Operations = new List<SchedulingOperationDetails>
            {
                new SchedulingOperationDetails(
                    opId,
                    Sequence: 10,
                    Name: "Assembly",
                    wcId,
                    SetupHours: 2m,
                    RunHoursPerUnit: 1m, // Total hours = 12
                    QueueTimeDays: 0,
                    MoveTimeDays: 0,
                    WorkCenterCapacityHoursPerDay: 8m,
                    WorkCenterIsFiniteCapacity: true)
            }
        };

        var service = new SchedulingService(dataProvider);

        // Request infinite capacity
        var result = await service.ScheduleAsync(
            new ScheduleRequest(orderId, CapacityMode.Infinite, SchedulingDirection.Forward),
            CancellationToken.None);

        var scheduledOp = Assert.Single(result.Operations);
        var expectedStart = new DateTimeOffset(today.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        var expectedEnd = new DateTimeOffset(today.ToDateTime(TimeOnly.MaxValue), TimeSpan.Zero);

        Assert.Equal(expectedStart, scheduledOp.StartUtc);
        Assert.Equal(expectedEnd, scheduledOp.EndUtc);
    }

    [Fact]
    public async Task ScheduleAsync_BackwardScheduling_SchedulesBackwardFromDueDate()
    {
        var orderId = Guid.NewGuid();
        var routingId = Guid.NewGuid();
        var wcId = Guid.NewGuid();
        var opId = Guid.NewGuid();

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var dueDate = today.AddDays(8);

        var dataProvider = new StubSchedulingDataProvider
        {
            Order = new SchedulingOrderDetails(orderId, Quantity: 5m, DueDate: dueDate, RoutingId: routingId),
            Operations = new List<SchedulingOperationDetails>
            {
                new SchedulingOperationDetails(
                    opId,
                    Sequence: 10,
                    Name: "Machining",
                    wcId,
                    SetupHours: 0m,
                    RunHoursPerUnit: 2m, // Total hours = 10
                    QueueTimeDays: 0,
                    MoveTimeDays: 0,
                    WorkCenterCapacityHoursPerDay: 8m,
                    WorkCenterIsFiniteCapacity: true)
            }
        };

        var service = new SchedulingService(dataProvider);

        var result = await service.ScheduleAsync(
            new ScheduleRequest(orderId, CapacityMode.Finite, SchedulingDirection.Backward),
            CancellationToken.None);

        // Backward scheduling:
        // Starts at DueDate (dueDate).
        // Day 1 (dueDate): Allocates 8 hours. EndDate = dueDate.
        // Day 2 (dueDate - 1 day): Allocates 2 hours. StartDate = dueDate - 1.
        var scheduledOp = Assert.Single(result.Operations);
        var expectedStart = new DateTimeOffset(dueDate.AddDays(-1).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        var expectedEnd = new DateTimeOffset(dueDate.ToDateTime(TimeOnly.MaxValue), TimeSpan.Zero);

        Assert.Equal(expectedStart, scheduledOp.StartUtc);
        Assert.Equal(expectedEnd, scheduledOp.EndUtc);
    }

    [Fact]
    public async Task GetWorkCenterLoadsAsync_CalculatesCorrectLoadPercentages()
    {
        var wcId1 = Guid.NewGuid();
        var wcId2 = Guid.NewGuid();

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var dataProvider = new StubSchedulingDataProvider
        {
            WorkCenters = new List<SchedulingWorkCenterDetails>
            {
                new SchedulingWorkCenterDetails(wcId1, "WC-1", "Assembly", CapacityHoursPerDay: 8m),
                new SchedulingWorkCenterDetails(wcId2, "WC-2", "Welding", CapacityHoursPerDay: 16m)
            },
            Allocations = new List<ExistingScheduleAllocation>
            {
                // WC-1 has 24 hours scheduled in the next 10 days
                new ExistingScheduleAllocation(wcId1, today.AddDays(1), AllocatedHours: 8m),
                new ExistingScheduleAllocation(wcId1, today.AddDays(2), AllocatedHours: 8m),
                new ExistingScheduleAllocation(wcId1, today.AddDays(3), AllocatedHours: 8m),
                
                // WC-2 has 80 hours scheduled in the next 10 days
                new ExistingScheduleAllocation(wcId2, today.AddDays(1), AllocatedHours: 16m),
                new ExistingScheduleAllocation(wcId2, today.AddDays(2), AllocatedHours: 16m),
                new ExistingScheduleAllocation(wcId2, today.AddDays(3), AllocatedHours: 16m),
                new ExistingScheduleAllocation(wcId2, today.AddDays(4), AllocatedHours: 16m),
                new ExistingScheduleAllocation(wcId2, today.AddDays(5), AllocatedHours: 16m),
                // Out of range (day 11)
                new ExistingScheduleAllocation(wcId2, today.AddDays(11), AllocatedHours: 16m)
            }
        };

        var service = new SchedulingService(dataProvider);

        var result = await service.GetWorkCenterLoadsAsync(rollingDays: 10, CancellationToken.None);

        // WC-1: capacity = 8 * 10 = 80. allocated = 24. load = (24/80)*100 = 30%.
        var load1 = Assert.Single(result, x => x.WorkCenterId == wcId1);
        Assert.Equal(80m, load1.CapacityHours);
        Assert.Equal(24m, load1.AllocatedHours);
        Assert.Equal(30m, load1.LoadPercentage);

        // WC-2: capacity = 16 * 10 = 160. allocated = 80. load = (80/160)*100 = 50%.
        var load2 = Assert.Single(result, x => x.WorkCenterId == wcId2);
        Assert.Equal(160m, load2.CapacityHours);
        Assert.Equal(80m, load2.AllocatedHours);
        Assert.Equal(50m, load2.LoadPercentage);
    }

    private sealed class StubSchedulingDataProvider : ISchedulingDataProvider
    {
        public SchedulingOrderDetails? Order { get; set; }
        public List<SchedulingOperationDetails> Operations { get; set; } = new();
        public List<ExistingScheduleAllocation> Allocations { get; set; } = new();
        public List<SchedulingWorkCenterDetails> WorkCenters { get; set; } = new();

        public DateOnly? SavedStartDate { get; private set; }
        public DateOnly? SavedEndDate { get; private set; }
        public IReadOnlyCollection<ScheduledOperation> SavedOperations { get; private set; } = Array.Empty<ScheduledOperation>();

        public Task<SchedulingOrderDetails?> GetOrderDetailsAsync(Guid productionOrderId, CancellationToken cancellationToken)
        {
            return Task.FromResult(Order);
        }

        public Task<IReadOnlyCollection<SchedulingOperationDetails>> GetRoutingOperationsAsync(Guid routingId, CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyCollection<SchedulingOperationDetails>>(Operations);
        }

        public Task<IReadOnlyCollection<ExistingScheduleAllocation>> GetExistingAllocationsAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyCollection<ExistingScheduleAllocation>>(Allocations);
        }

        public Task<IReadOnlyCollection<SchedulingWorkCenterDetails>> GetWorkCentersAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyCollection<SchedulingWorkCenterDetails>>(WorkCenters);
        }

        public Task SaveScheduleAsync(
            Guid productionOrderId,
            DateOnly? scheduledStartDate,
            DateOnly? scheduledEndDate,
            IReadOnlyCollection<ScheduledOperation> operations,
            CancellationToken cancellationToken)
        {
            SavedStartDate = scheduledStartDate;
            SavedEndDate = scheduledEndDate;
            SavedOperations = operations;
            return Task.CompletedTask;
        }
    }
}
