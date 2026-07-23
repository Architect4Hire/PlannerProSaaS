namespace PlannerPro.Gateway.Tenancy;

/// <summary>
/// Bound from the "Tenancy" config section. Short TTLs are the entire cache-invalidation mechanism
/// today (ADR-0011: "the TTL is a deliberate exposure window that must be short and stated") — there
/// is no <c>MembershipChanged</c> event or consumer yet for push-based invalidation to react to.
/// </summary>
public sealed class TenantDirectoryOptions
{
    public TimeSpan TenantCacheTtl { get; set; } = TimeSpan.FromSeconds(30);

    public TimeSpan MembershipCacheTtl { get; set; } = TimeSpan.FromSeconds(30);
}
