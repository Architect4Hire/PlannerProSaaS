using Microsoft.EntityFrameworkCore;
using PlannerPro.Shared.Tests.TestSupport;

namespace PlannerPro.Shared.Tests.Tenancy;

public sealed class TenantQueryFilterTests : IDisposable
{
    private readonly SqliteTestDbContextFactory _factory = new();

    [Fact]
    public async Task Query_ScopedToTenantA_DoesNotSeeTenantBsRow()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        await SeedAsync(tenantA, "a-row");
        await SeedAsync(tenantB, "b-row");

        await using var contextA = _factory.CreateContext(StaticTenantContext.For(tenantA));
        await using var contextB = _factory.CreateContext(StaticTenantContext.For(tenantB));

        var rowsSeenByA = await contextA.TenantScopedEntities.ToListAsync();
        var rowsSeenByB = await contextB.TenantScopedEntities.ToListAsync();

        var rowA = Assert.Single(rowsSeenByA);
        Assert.Equal("a-row", rowA.Name);

        var rowB = Assert.Single(rowsSeenByB);
        Assert.Equal("b-row", rowB.Name);
    }

    [Fact]
    public async Task Query_ScopedToBypassContext_SeesBothTenantsRows()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        await SeedAsync(tenantA, "a-row");
        await SeedAsync(tenantB, "b-row");

        await using var systemContext = _factory.CreateContext(StaticTenantContext.Bypass);

        var allRows = await systemContext.TenantScopedEntities.ToListAsync();

        Assert.Equal(2, allRows.Count);
        Assert.Contains(allRows, r => r.Name == "a-row");
        Assert.Contains(allRows, r => r.Name == "b-row");
    }

    private async Task SeedAsync(Guid tenantId, string name)
    {
        await using var seedContext = _factory.CreateContext(StaticTenantContext.For(tenantId));
        seedContext.TenantScopedEntities.Add(new TenantScopedEntity { Id = Guid.NewGuid(), Name = name });
        await seedContext.SaveChangesAsync();
    }

    public void Dispose() => _factory.Dispose();
}
