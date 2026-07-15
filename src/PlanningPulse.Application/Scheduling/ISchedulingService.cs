using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace PlanningPulse.Application.Scheduling;

public interface ISchedulingService
{
    Task<ScheduleResult> ScheduleAsync(ScheduleRequest request, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<WorkCenterLoad>> GetWorkCenterLoadsAsync(int rollingDays, CancellationToken cancellationToken);
}
