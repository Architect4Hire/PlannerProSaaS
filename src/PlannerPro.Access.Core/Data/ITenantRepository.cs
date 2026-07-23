using PlannerPro.Access.Core.Managers.Models.Domain;

namespace PlannerPro.Access.Core.Data;

public interface ITenantRepository
{
    Task<Tenant?> FindBySlugAsync(string slug, CancellationToken ct = default);
}
