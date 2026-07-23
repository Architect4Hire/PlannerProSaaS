using PlannerPro.Access.Core.Data;
using PlannerPro.Access.Core.Managers.Models.Domain;

namespace PlannerPro.Access.Tests.TestSupport;

internal sealed class StubTenantRepository : ITenantRepository
{
    public Tenant? Tenant { get; set; }

    public Task<Tenant?> FindBySlugAsync(string slug, CancellationToken ct = default) =>
        Task.FromResult(Tenant?.Slug == slug ? Tenant : null);
}
