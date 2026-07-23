using Microsoft.EntityFrameworkCore;
using PlannerPro.Access.Core.Managers.Models.Domain;
using PlannerPro.Shared.Tenancy;

namespace PlannerPro.Access.Core.Data;

/// <summary>
/// Backs the Gateway's internal tenant-by-slug resolution — the one place in Access that reads
/// <see cref="Tenant"/> without an ambient resolved tenant, because resolving the tenant is exactly
/// what hasn't happened yet on this path. Builds its own <see cref="AccessDbContext"/> per call under
/// an explicit <see cref="SystemTenantContext"/> bypass, rather than using the request-scoped context
/// DI would otherwise inject (which, unresolved, would filter out every row) — the third named use of
/// that bypass category alongside migration and seeding; see the XML doc on <see cref="Tenant"/> and
/// on <see cref="SystemTenantContext"/> itself.
/// </summary>
/// <remarks>
/// Takes the DI-registered <see cref="DbContextOptions{TContext}"/> (from <c>AddAccessCore</c>'s
/// <c>services.AddDbContext&lt;AccessDbContext&gt;(...)</c> — see that type's remarks for why it's
/// plain <c>AddDbContext</c> reading the Aspire-injected connection string, not the pooled
/// <c>Aspire.Microsoft.EntityFrameworkCore.SqlServer</c> package) and constructs a fresh
/// <see cref="AccessDbContext"/> bound to the bypass context instead of the ambient one. This is safe
/// to reuse: <see cref="Shared.Persistence.SharedDbContext.OnConfiguring"/> wires
/// <c>TenantSaveChangesInterceptor</c> itself from whichever <see cref="ITenantContext"/> was passed to
/// THIS context instance's constructor — the bypass below — so there is no risk of it picking up the
/// ambient request's interceptor by mistake.
/// </remarks>
public sealed class TenantRepository(DbContextOptions<AccessDbContext> options) : ITenantRepository
{
    public async Task<Tenant?> FindBySlugAsync(string slug, CancellationToken ct = default)
    {
        await using var context = new AccessDbContext(options, new SystemTenantContext());

        // FirstOrDefaultAsync, never FindAsync — Find bypasses query filters on tracked entities; here
        // BypassFilters is already true, but the rule is followed unconditionally so this stays a safe
        // pattern to copy elsewhere.
        return await context.Tenants.FirstOrDefaultAsync(t => t.Slug == slug, ct);
    }
}
