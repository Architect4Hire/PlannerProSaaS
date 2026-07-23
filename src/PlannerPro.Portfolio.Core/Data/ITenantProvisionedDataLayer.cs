using PlannerPro.Contracts;

namespace PlannerPro.Portfolio.Core.Data;

public interface ITenantProvisionedDataLayer
{
    Task ProvisionInternalClientAsync(TenantProvisioned provisionedEvent, CancellationToken ct = default);
}
