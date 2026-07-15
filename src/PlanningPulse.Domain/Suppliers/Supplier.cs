using PlanningPulse.Domain.Common;

namespace PlanningPulse.Domain.Suppliers;

public sealed class Supplier : Entity, ITenantOwned
{
    public Guid TenantId { get; set; }
    public string SupplierNumber { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Email { get; set; }
    public bool IsActive { get; set; } = true;
}
