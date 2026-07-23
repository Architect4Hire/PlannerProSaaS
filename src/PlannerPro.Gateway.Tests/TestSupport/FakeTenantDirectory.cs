using PlannerPro.Gateway.Tenancy;

namespace PlannerPro.Gateway.Tests.TestSupport;

/// <summary>
/// Hand-written fake <see cref="ITenantDirectory"/> — matches this repo's existing convention (no
/// mocking library is used anywhere in the solution). Lets tests set up exactly the tenant/membership
/// combinations they need without any HTTP call or a real Access instance.
/// </summary>
public sealed class FakeTenantDirectory : ITenantDirectory
{
    private readonly Dictionary<string, TenantLookup> _tenantsBySlug = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<(Guid TenantId, Guid ActorId), MembershipLookup> _memberships = [];

    public int ResolveTenantCallCount { get; private set; }

    public int ResolveMembershipCallCount { get; private set; }

    public void AddTenant(TenantLookup tenant) => _tenantsBySlug[tenant.Slug] = tenant;

    public void AddMembership(Guid tenantId, Guid actorId, MembershipLookup membership) =>
        _memberships[(tenantId, actorId)] = membership;

    public Task<TenantLookup?> ResolveTenantAsync(string slug, CancellationToken cancellationToken)
    {
        ResolveTenantCallCount++;
        return Task.FromResult(_tenantsBySlug.GetValueOrDefault(slug));
    }

    public Task<MembershipLookup?> ResolveMembershipAsync(Guid tenantId, Guid actorId, CancellationToken cancellationToken)
    {
        ResolveMembershipCallCount++;
        return Task.FromResult(_memberships.GetValueOrDefault((tenantId, actorId)));
    }
}
