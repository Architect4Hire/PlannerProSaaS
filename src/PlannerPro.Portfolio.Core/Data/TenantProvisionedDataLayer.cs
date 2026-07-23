using PlannerPro.Contracts;

namespace PlannerPro.Portfolio.Core.Data;

/// <summary>Composes the one repository call this operation needs — kept as its own layer so
/// Portfolio's shape matches every other service's (Controller/Consumer → Facade → Business →
/// DataLayer → Repository), per <c>.claude/rules/backend.md</c>.</summary>
public sealed class TenantProvisionedDataLayer(IClientRepository repository) : ITenantProvisionedDataLayer
{
    public Task ProvisionInternalClientAsync(TenantProvisioned provisionedEvent, CancellationToken ct = default) =>
        repository.ProvisionInternalClientAsync(provisionedEvent, ct);
}
