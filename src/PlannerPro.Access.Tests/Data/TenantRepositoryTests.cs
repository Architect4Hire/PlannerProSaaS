using Microsoft.EntityFrameworkCore;
using PlannerPro.Access.Core.Data;
using PlannerPro.Access.Core.Managers.Models.Domain;
using PlannerPro.Access.Tests.TestSupport;

namespace PlannerPro.Access.Tests.Data;

public sealed class TenantRepositoryTests : IDisposable
{
    private readonly SqliteAccessDbContextFactory _factory = new();

    [Fact]
    public async Task FindBySlugAsync_KnownSlug_ReturnsTenantAcrossTenantBoundaries()
    {
        var tenantId = Guid.NewGuid();
        using (var seedContext = _factory.CreateContext(StaticTenantContext.For(tenantId)))
        {
            seedContext.Tenants.Add(new Tenant { Id = tenantId, Slug = "acme", Name = "Acme" });
            await seedContext.SaveChangesAsync();
        }

        var repository = new TenantRepository(BuildOptions());

        // Called with no ambient tenant resolved at all — the whole point of this repository — so a
        // caller from a DIFFERENT tenant, or no tenant, must still find the row.
        var found = await repository.FindBySlugAsync("acme");

        Assert.NotNull(found);
        Assert.Equal(tenantId, found!.TenantId);
    }

    [Fact]
    public async Task FindBySlugAsync_UnknownSlug_ReturnsNull()
    {
        // The schema is created lazily by CreateContext's EnsureCreated() — this test doesn't seed a
        // Tenant row, but still needs the table to exist before the repository queries it.
        using (_factory.CreateContext())
        {
        }

        var repository = new TenantRepository(BuildOptions());

        var found = await repository.FindBySlugAsync("no-such-tenant");

        Assert.Null(found);
    }

    private DbContextOptions<AccessDbContext> BuildOptions() =>
        new DbContextOptionsBuilder<AccessDbContext>().UseSqlite(_factory.Connection).Options;

    public void Dispose() => _factory.Dispose();
}
