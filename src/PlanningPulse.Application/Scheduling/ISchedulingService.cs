namespace PlanningPulse.Application.Scheduling;

public interface ISchedulingService
{
    Task<ScheduleResult> ScheduleAsync(ScheduleRequest request, CancellationToken cancellationToken);
}
