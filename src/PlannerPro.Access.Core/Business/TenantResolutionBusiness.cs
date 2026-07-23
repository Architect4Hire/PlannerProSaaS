using PlannerPro.Access.Core.Data;
using PlannerPro.Access.Core.Managers.Models.ServiceModels;

namespace PlannerPro.Access.Core.Business;

public sealed class TenantResolutionBusiness(ITenantResolutionDataLayer dataLayer) : ITenantResolutionBusiness
{
    public async Task<TenantLookupServiceModel?> ResolveTenantAsync(string slug, CancellationToken ct = default)
    {
        var tenant = await dataLayer.GetTenantBySlugAsync(slug, ct);
        if (tenant is null) return null;

        // Plan is left null until Billing (which owns the Plan catalog) exists — Tenant.PlanId is a
        // bare Guid? with no local catalog to resolve a code/name from yet.
        return new TenantLookupServiceModel(tenant.TenantId, tenant.Slug, tenant.Status.ToString(), null);
    }

    public async Task<MembershipLookupServiceModel?> ResolveMembershipAsync(Guid tenantId, Guid actorId, CancellationToken ct = default)
    {
        var membership = await dataLayer.GetActiveMembershipAsync(tenantId, actorId, ct);
        return membership is null ? null : new MembershipLookupServiceModel(membership.Role.ToString());
    }
}
