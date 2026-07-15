using PlanningPulse.Application.Scheduling;

namespace PlanningPulse.Tests.Scheduling;

public sealed class SchedulingServiceTests
{
    [Fact]
    public async Task ScheduleAsync_PhaseOneScaffold_ReturnsOrderId()
    {
        var orderId = Guid.NewGuid();
        var service = new SchedulingService();

        var result = await service.ScheduleAsync(
            new ScheduleRequest(orderId, CapacityMode.Finite, SchedulingDirection.Forward),
            CancellationToken.None);

        Assert.Equal(orderId, result.ProductionOrderId);
        Assert.Empty(result.Operations);
    }
}
