using PlannerPro.Contracts;

namespace PlannerPro.Portfolio.Core.Facade;

public interface IClientFacade
{
    Task ProvisionInternalClientAsync(TenantProvisioned provisionedEvent, CancellationToken ct = default);
}
