using PlannerPro.Shared.Tenancy;

namespace PlannerPro.Shared.Tests.TestSupport;

internal sealed class TenantScopedEntity : ITenantScoped
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string Name { get; set; } = "";
}
