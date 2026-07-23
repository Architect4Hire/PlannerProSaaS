using PlannerPro.Contracts;
using PlannerPro.Portfolio.Core.Business;

namespace PlannerPro.Portfolio.Core.Facade;

public sealed class ClientFacade(ITenantProvisionedBusiness business) : IClientFacade
{
    public Task ProvisionInternalClientAsync(TenantProvisioned provisionedEvent, CancellationToken ct = default) =>
        business.HandleAsync(provisionedEvent, ct);
}
