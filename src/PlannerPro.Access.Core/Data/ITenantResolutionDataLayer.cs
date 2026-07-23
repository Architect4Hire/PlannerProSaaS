using PlannerPro.Access.Core.Managers.Models.Domain;

namespace PlannerPro.Access.Core.Data;

public interface ITenantResolutionDataLayer
{
    Task<Tenant?> GetTenantBySlugAsync(string slug, CancellationToken ct = default);

    Task<TenantMembership?> GetActiveMembershipAsync(Guid tenantId, Guid userId, CancellationToken ct = default);
}
