using Microsoft.EntityFrameworkCore;
using PlannerPro.Shared.Exceptions;
using PlannerPro.Shared.Tests.TestSupport;

namespace PlannerPro.Shared.Tests.Tenancy;

public sealed class TenantSaveChangesInterceptorTests : IDisposable
{
    private readonly SqliteTestDbContextFactory _factory = new();

    [Fact]
    public async Task Add_WithoutExplicitTenantId_IsStampedFromTheCurrentTenant()
    {
        var tenantA = Guid.NewGuid();
        await using var context = _factory.CreateContext(StaticTenantContext.For(tenantA));

        var entity = new TenantScopedEntity { Id = Guid.NewGuid(), Name = "new" };
        context.TenantScopedEntities.Add(entity);
        await context.SaveChangesAsync();

        Assert.Equal(tenantA, entity.TenantId);
    }

    [Fact]
    public async Task Modify_AnotherTenantsRow_ThrowsCrossTenantWriteException()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var entityId = await SeedAsync(tenantB, "b-row");

        await using var contextA = _factory.CreateContext(StaticTenantContext.For(tenantA));
        var tracked = AttachUnfiltered(contextA, entityId, tenantB, "b-row");
        tracked.Name = "tampered";
        contextA.Entry(tracked).State = EntityState.Modified;

        await Assert.ThrowsAsync<CrossTenantWriteException>(() => contextA.SaveChangesAsync());
    }

    [Fact]
    public async Task Delete_AnotherTenantsRow_ThrowsCrossTenantWriteException()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var entityId = await SeedAsync(tenantB, "b-row");

        await using var contextA = _factory.CreateContext(StaticTenantContext.For(tenantA));
        var tracked = AttachUnfiltered(contextA, entityId, tenantB, "b-row");
        contextA.Entry(tracked).State = EntityState.Deleted;

        await Assert.ThrowsAsync<CrossTenantWriteException>(() => contextA.SaveChangesAsync());
    }

    [Fact]
    public async Task Modify_ReassigningOwnRowToAnotherTenant_ThrowsCrossTenantWriteException()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var entityId = await SeedAsync(tenantA, "a-row");

        await using var contextA = _factory.CreateContext(StaticTenantContext.For(tenantA));
        var tracked = AttachUnfiltered(contextA, entityId, tenantA, "a-row");
        tracked.TenantId = tenantB;
        contextA.Entry(tracked).State = EntityState.Modified;

        await Assert.ThrowsAsync<CrossTenantWriteException>(() => contextA.SaveChangesAsync());
    }

    [Fact]
    public async Task ModifyOrDelete_AGuidEmptyTenantRow_WhileUnresolved_ThrowsRatherThanSilentlyMatching()
    {
        // Simulates a row that reached the table with TenantId == Guid.Empty by some path outside the
        // interceptor (raw SQL, bulk import). Without the IsResolved check, an unresolved context's
        // own TenantId (also Guid.Empty) would match it trivially and the write would silently succeed.
        var unresolved = new StaticTenantContext(); // IsResolved = false, BypassFilters = false
        await using var context = _factory.CreateContext(unresolved);

        var leaked = new TenantScopedEntity { Id = Guid.NewGuid(), TenantId = Guid.Empty, Name = "leaked" };
        context.Attach(leaked);
        leaked.Name = "tampered";
        context.Entry(leaked).State = EntityState.Modified;

        await Assert.ThrowsAsync<InvalidOperationException>(() => context.SaveChangesAsync());
    }

    [Fact]
    public async Task Add_UnderBypassContext_WithoutExplicitTenantId_Throws()
    {
        await using var systemContext = _factory.CreateContext(StaticTenantContext.Bypass);

        systemContext.TenantScopedEntities.Add(new TenantScopedEntity { Id = Guid.NewGuid(), Name = "orphan" });

        await Assert.ThrowsAsync<InvalidOperationException>(() => systemContext.SaveChangesAsync());
    }

    [Fact]
    public async Task Add_WhileUnresolvedAndNotBypassing_Throws()
    {
        var unresolved = new StaticTenantContext(); // IsResolved = false, BypassFilters = false
        await using var context = _factory.CreateContext(unresolved);

        context.TenantScopedEntities.Add(new TenantScopedEntity { Id = Guid.NewGuid(), Name = "no-scope" });

        await Assert.ThrowsAsync<InvalidOperationException>(() => context.SaveChangesAsync());
    }

    private async Task<Guid> SeedAsync(Guid tenantId, string name)
    {
        var id = Guid.NewGuid();
        await using var seedContext = _factory.CreateContext(StaticTenantContext.For(tenantId));
        seedContext.TenantScopedEntities.Add(new TenantScopedEntity { Id = id, Name = name });
        await seedContext.SaveChangesAsync();
        return id;
    }

    /// <summary>
    /// Simulates a foreign-tenant row landing in this context's change tracker despite the query
    /// filter — e.g. a stale reference, a raw-SQL projection, or an attached graph — which is exactly
    /// the hole the interceptor exists to close (a query filter alone can't catch this).
    /// </summary>
    private static TenantScopedEntity AttachUnfiltered(TestDbContext context, Guid id, Guid tenantId, string name)
    {
        var entity = new TenantScopedEntity { Id = id, TenantId = tenantId, Name = name };
        context.Attach(entity);
        return entity;
    }

    public void Dispose() => _factory.Dispose();
}
