using Microsoft.EntityFrameworkCore;
using PlannerPro.Access.Core.Managers.Models.Domain;
using PlannerPro.Shared.Tenancy;

namespace PlannerPro.Access.Core.Data;

/// <summary>
/// Backs the Gateway's internal membership resolution — same bypass reasoning as
/// <see cref="TenantRepository"/>, including why it's safe to reuse the DI-registered
/// <see cref="DbContextOptions{TContext}"/> (see that type's remarks).
/// </summary>
public sealed class TenantMembershipRepository(DbContextOptions<AccessDbContext> options) : ITenantMembershipRepository
{
    public async Task<TenantMembership?> FindActiveAsync(Guid tenantId, Guid userId, CancellationToken ct = default)
    {
        await using var context = new AccessDbContext(options, new SystemTenantContext());

        // Explicit TenantId/Status predicates, not the automatic query filter (which is bypassed under
        // SystemTenantContext) — this is the one place in Access that has to filter by hand, and only
        // because it's establishing tenant scope rather than operating inside it.
        return await context.TenantMemberships.FirstOrDefaultAsync(
            m => m.TenantId == tenantId && m.UserId == userId && m.Status == MembershipStatus.Active, ct);
    }
}
