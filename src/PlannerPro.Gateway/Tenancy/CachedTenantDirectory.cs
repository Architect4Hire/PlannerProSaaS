using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace PlannerPro.Gateway.Tenancy;

/// <summary>
/// Wraps an inner <see cref="ITenantDirectory"/> with a short-TTL, per-instance <see cref="IMemoryCache"/>.
/// Cache keys are scoped by every dimension that scopes the row (slug for a tenant, tenant+actor for a
/// membership) per the cache-key rule in the high-level design. <see cref="InvalidateMembership"/> is
/// unused today but already wired so a future bus-driven invalidation path (once Access publishes a
/// membership-change event and something consumes it here) has an obvious place to call into, rather
/// than needing to be retrofitted.
/// </summary>
public sealed class CachedTenantDirectory(
    ITenantDirectory inner, IMemoryCache cache, IOptions<TenantDirectoryOptions> options) : ITenantDirectory
{
    private readonly TenantDirectoryOptions _options = options.Value;

    public async Task<TenantLookup?> ResolveTenantAsync(string slug, CancellationToken cancellationToken)
    {
        var key = TenantCacheKey(slug);
        if (cache.TryGetValue(key, out TenantLookup? cached))
        {
            return cached;
        }

        var result = await inner.ResolveTenantAsync(slug, cancellationToken);
        cache.Set(key, result, _options.TenantCacheTtl);
        return result;
    }

    public async Task<MembershipLookup?> ResolveMembershipAsync(Guid tenantId, Guid actorId, CancellationToken cancellationToken)
    {
        var key = MembershipCacheKey(tenantId, actorId);
        if (cache.TryGetValue(key, out MembershipLookup? cached))
        {
            return cached;
        }

        var result = await inner.ResolveMembershipAsync(tenantId, actorId, cancellationToken);
        cache.Set(key, result, _options.MembershipCacheTtl);
        return result;
    }

    public void InvalidateMembership(Guid tenantId, Guid actorId) =>
        cache.Remove(MembershipCacheKey(tenantId, actorId));

    private static string TenantCacheKey(string slug) => $"tenant:slug:{slug}";

    private static string MembershipCacheKey(Guid tenantId, Guid actorId) => $"membership:{tenantId}:{actorId}";
}
