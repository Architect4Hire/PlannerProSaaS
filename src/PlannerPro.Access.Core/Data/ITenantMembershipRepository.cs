using PlannerPro.Access.Core.Managers.Models.Domain;

namespace PlannerPro.Access.Core.Data;

public interface ITenantMembershipRepository
{
    Task<TenantMembership?> FindActiveAsync(Guid tenantId, Guid userId, CancellationToken ct = default);
}
