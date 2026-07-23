using PlannerPro.Contracts;

namespace PlannerPro.Portfolio.Core.Business;

public interface ITenantProvisionedBusiness
{
    Task HandleAsync(TenantProvisioned provisionedEvent, CancellationToken ct = default);
}
