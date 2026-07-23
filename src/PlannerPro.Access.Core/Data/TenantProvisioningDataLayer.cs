using PlannerPro.Access.Core.Managers.Models.Domain;
using PlannerPro.Contracts;

namespace PlannerPro.Access.Core.Data;

/// <summary>
/// Composes the one repository call this operation needs. Kept as its own layer — rather than having
/// the business call the repository directly — so every Access operation has the same shape
/// (Controller → Facade → Business → DataLayer → Repository), per <c>.claude/rules/backend.md</c>.
/// </summary>
public sealed class TenantProvisioningDataLayer(ITenantProvisioningRepository repository) : ITenantProvisioningDataLayer
{
    public Task ProvisionAsync(
        Tenant tenant,
        TenantSettings settings,
        TenantBranding branding,
        TenantMembership ownerMembership,
        TenantProvisioned provisionedEvent,
        CancellationToken ct = default) =>
        repository.ProvisionAsync(tenant, settings, branding, ownerMembership, provisionedEvent, ct);
}
