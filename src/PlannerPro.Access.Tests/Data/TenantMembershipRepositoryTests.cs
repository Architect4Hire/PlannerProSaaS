using Microsoft.EntityFrameworkCore;
using PlannerPro.Access.Core.Data;
using PlannerPro.Access.Core.Managers.Models.Domain;
using PlannerPro.Access.Tests.TestSupport;

namespace PlannerPro.Access.Tests.Data;

public sealed class TenantMembershipRepositoryTests : IDisposable
{
    private readonly SqliteAccessDbContextFactory _factory = new();

    [Fact]
    public async Task FindActiveAsync_ActiveMember_ReturnsMembership()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        using (var seedContext = _factory.CreateContext(StaticTenantContext.For(tenantId)))
        {
            seedContext.Tenants.Add(new Tenant { Id = tenantId, Slug = "acme-1", Name = "Acme" });
            await seedContext.SaveChangesAsync();

            seedContext.TenantMemberships.Add(new TenantMembership
            {
                Id = Guid.NewGuid(), UserId = userId, Role = TenantRole.Admin, Status = MembershipStatus.Active,
            });
            await seedContext.SaveChangesAsync();
        }

        var repository = new TenantMembershipRepository(BuildOptions());
        var found = await repository.FindActiveAsync(tenantId, userId);

        Assert.NotNull(found);
        Assert.Equal(TenantRole.Admin, found!.Role);
    }

    [Fact]
    public async Task FindActiveAsync_RemovedMember_ReturnsNull()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        using (var seedContext = _factory.CreateContext(StaticTenantContext.For(tenantId)))
        {
            seedContext.Tenants.Add(new Tenant { Id = tenantId, Slug = "acme-2", Name = "Acme" });
            await seedContext.SaveChangesAsync();

            seedContext.TenantMemberships.Add(new TenantMembership
            {
                Id = Guid.NewGuid(), UserId = userId, Role = TenantRole.Admin, Status = MembershipStatus.Removed,
            });
            await seedContext.SaveChangesAsync();
        }

        var repository = new TenantMembershipRepository(BuildOptions());
        var found = await repository.FindActiveAsync(tenantId, userId);

        Assert.Null(found);
    }

    [Fact]
    public async Task FindActiveAsync_NoSuchMembership_ReturnsNull()
    {
        // The schema is created lazily by CreateContext's EnsureCreated() — this test doesn't seed
        // anything, but still needs the table to exist before the repository queries it.
        using (_factory.CreateContext())
        {
        }

        var repository = new TenantMembershipRepository(BuildOptions());

        var found = await repository.FindActiveAsync(Guid.NewGuid(), Guid.NewGuid());

        Assert.Null(found);
    }

    private DbContextOptions<AccessDbContext> BuildOptions() =>
        new DbContextOptionsBuilder<AccessDbContext>().UseSqlite(_factory.Connection).Options;

    public void Dispose() => _factory.Dispose();
}
