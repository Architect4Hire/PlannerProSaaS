using PlannerPro.Access.Core.Managers.Models.ServiceModels;

namespace PlannerPro.Access.Core.Facade;

public interface ITenantResolutionFacade
{
    Task<TenantLookupServiceModel?> ResolveTenantAsync(string slug, CancellationToken ct = default);

    Task<MembershipLookupServiceModel?> ResolveMembershipAsync(Guid tenantId, Guid actorId, CancellationToken ct = default);
}
