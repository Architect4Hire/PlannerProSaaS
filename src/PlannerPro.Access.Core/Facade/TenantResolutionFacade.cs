using PlannerPro.Access.Core.Business;
using PlannerPro.Access.Core.Managers.Models.ServiceModels;

namespace PlannerPro.Access.Core.Facade;

/// <summary>
/// No FluentValidation ViewModel here — unlike <see cref="IAuthFacade"/>, this surface takes route
/// parameters from the Gateway's internal, non-client-facing resolution calls, not a client-submitted
/// body. No caching yet either; the Gateway's own <c>CachedTenantDirectory</c> already caches these
/// results with a short TTL, so adding a second cache here would just be redundant, not defensive.
/// </summary>
public sealed class TenantResolutionFacade(ITenantResolutionBusiness business) : ITenantResolutionFacade
{
    public Task<TenantLookupServiceModel?> ResolveTenantAsync(string slug, CancellationToken ct = default) =>
        string.IsNullOrWhiteSpace(slug)
            ? Task.FromResult<TenantLookupServiceModel?>(null)
            : business.ResolveTenantAsync(slug, ct);

    public Task<MembershipLookupServiceModel?> ResolveMembershipAsync(Guid tenantId, Guid actorId, CancellationToken ct = default) =>
        business.ResolveMembershipAsync(tenantId, actorId, ct);
}
