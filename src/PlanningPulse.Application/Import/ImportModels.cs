using System.Collections.Generic;

namespace PlanningPulse.Application.Import;

public sealed record ImportRowError(int RowNumber, string ErrorMessage);
public sealed record ImportResult(bool Success, int CreatedCount, int UpdatedCount, IReadOnlyCollection<ImportRowError> Errors);
