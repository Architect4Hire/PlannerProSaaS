using PlannerPro.Access.Core.Managers.Models.Domain;

namespace PlannerPro.Access.Core.Data;

/// <summary>
/// Pure reads, so there's no outbox row to enqueue and no transaction to compose — the layer exists
/// anyway to keep every service's shape the same (Controller → Facade → Business → DataLayer →
/// Repository), which is the point of Access being the template.
/// </summary>
public sealed class TenantResolutionDataLayer(
    ITenantRepository tenantRepository, ITenantMembershipRepository membershipRepository)
    : ITenantResolutionDataLayer
{
    public Task<Tenant?> GetTenantBySlugAsync(string slug, CancellationToken ct = default) =>
        tenantRepository.FindBySlugAsync(slug, ct);

    public Task<TenantMembership?> GetActiveMembershipAsync(Guid tenantId, Guid userId, CancellationToken ct = default) =>
        membershipRepository.FindActiveAsync(tenantId, userId, ct);
}
