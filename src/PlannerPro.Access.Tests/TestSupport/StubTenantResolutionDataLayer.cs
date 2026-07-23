using PlannerPro.Access.Core.Data;
using PlannerPro.Access.Core.Managers.Models.Domain;

namespace PlannerPro.Access.Tests.TestSupport;

internal sealed class StubTenantResolutionDataLayer : ITenantResolutionDataLayer
{
    public Tenant? Tenant { get; set; }

    public TenantMembership? Membership { get; set; }

    public Task<Tenant?> GetTenantBySlugAsync(string slug, CancellationToken ct = default) =>
        Task.FromResult(Tenant?.Slug == slug ? Tenant : null);

    public Task<TenantMembership?> GetActiveMembershipAsync(Guid tenantId, Guid userId, CancellationToken ct = default) =>
        Task.FromResult(Membership?.TenantId == tenantId && Membership?.UserId == userId ? Membership : null);
}
