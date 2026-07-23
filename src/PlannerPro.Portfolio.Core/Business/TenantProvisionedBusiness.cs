using PlannerPro.Contracts;
using PlannerPro.Portfolio.Core.Data;

namespace PlannerPro.Portfolio.Core.Business;

/// <summary>No domain rules beyond "create one client named Internal" — exists to keep this
/// operation's shape consistent with every other service's, per <c>.claude/rules/backend.md</c>.</summary>
public sealed class TenantProvisionedBusiness(ITenantProvisionedDataLayer dataLayer) : ITenantProvisionedBusiness
{
    public Task HandleAsync(TenantProvisioned provisionedEvent, CancellationToken ct = default) =>
        dataLayer.ProvisionInternalClientAsync(provisionedEvent, ct);
}
