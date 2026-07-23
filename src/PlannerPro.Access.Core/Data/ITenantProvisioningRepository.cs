using PlannerPro.Access.Core.Managers.Models.Domain;
using PlannerPro.Contracts;

namespace PlannerPro.Access.Core.Data;

public interface ITenantProvisioningRepository
{
    Task ProvisionAsync(
        Tenant tenant,
        TenantSettings settings,
        TenantBranding branding,
        TenantMembership ownerMembership,
        TenantProvisioned provisionedEvent,
        CancellationToken ct = default);
}
