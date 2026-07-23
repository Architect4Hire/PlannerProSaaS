using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using PlannerPro.Gateway.Tenancy;
using PlannerPro.Gateway.Tests.TestSupport;

namespace PlannerPro.Gateway.Tests.Tenancy;

public sealed class CachedTenantDirectoryTests
{
    private static CachedTenantDirectory CreateSubject(FakeTenantDirectory inner, TimeSpan ttl) =>
        new(inner, new MemoryCache(new MemoryCacheOptions()),
            Options.Create(new TenantDirectoryOptions { TenantCacheTtl = ttl, MembershipCacheTtl = ttl }));

    [Fact]
    public async Task ResolveTenantAsync_SecondCallWithinTtl_DoesNotHitInnerDirectoryAgain()
    {
        var inner = new FakeTenantDirectory();
        inner.AddTenant(new TenantLookup(Guid.NewGuid(), "acme", "Active", "team"));
        var subject = CreateSubject(inner, TimeSpan.FromSeconds(30));

        await subject.ResolveTenantAsync("acme", CancellationToken.None);
        await subject.ResolveTenantAsync("acme", CancellationToken.None);

        Assert.Equal(1, inner.ResolveTenantCallCount);
    }

    [Fact]
    public async Task ResolveTenantAsync_AfterTtlExpires_HitsInnerDirectoryAgain()
    {
        var inner = new FakeTenantDirectory();
        inner.AddTenant(new TenantLookup(Guid.NewGuid(), "acme", "Active", "team"));
        var subject = CreateSubject(inner, TimeSpan.FromMilliseconds(20));

        await subject.ResolveTenantAsync("acme", CancellationToken.None);
        await Task.Delay(TimeSpan.FromMilliseconds(200));
        await subject.ResolveTenantAsync("acme", CancellationToken.None);

        Assert.Equal(2, inner.ResolveTenantCallCount);
    }

    [Fact]
    public async Task ResolveMembershipAsync_CacheKeyIsScopedByTenantAndActor()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var actor = Guid.NewGuid();
        var inner = new FakeTenantDirectory();
        inner.AddMembership(tenantA, actor, new MembershipLookup("Admin"));
        inner.AddMembership(tenantB, actor, new MembershipLookup("Member"));
        var subject = CreateSubject(inner, TimeSpan.FromSeconds(30));

        var membershipA = await subject.ResolveMembershipAsync(tenantA, actor, CancellationToken.None);
        var membershipB = await subject.ResolveMembershipAsync(tenantB, actor, CancellationToken.None);

        Assert.Equal("Admin", membershipA!.Role);
        Assert.Equal("Member", membershipB!.Role);
        Assert.Equal(2, inner.ResolveMembershipCallCount);
    }

    [Fact]
    public async Task InvalidateMembership_ClearsTheCachedEntry()
    {
        var tenantId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var inner = new FakeTenantDirectory();
        inner.AddMembership(tenantId, actorId, new MembershipLookup("Member"));
        var subject = CreateSubject(inner, TimeSpan.FromSeconds(30));

        await subject.ResolveMembershipAsync(tenantId, actorId, CancellationToken.None);
        subject.InvalidateMembership(tenantId, actorId);
        await subject.ResolveMembershipAsync(tenantId, actorId, CancellationToken.None);

        Assert.Equal(2, inner.ResolveMembershipCallCount);
    }
}
