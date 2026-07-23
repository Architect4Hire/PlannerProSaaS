using Microsoft.EntityFrameworkCore;
using PlannerPro.Access.Core.Data;
using PlannerPro.Access.Core.Managers.Models.Domain;
using PlannerPro.Access.Tests.TestSupport;
using PlannerPro.Contracts;

namespace PlannerPro.Access.Tests.Data;

public sealed class TenantProvisioningRepositoryTests : IDisposable
{
    private readonly SqliteAccessDbContextFactory _factory = new();

    [Fact]
    public async Task ProvisionAsync_PersistsAllFourEntitiesAndTheOutboxRowInOneTransaction()
    {
        // Schema created lazily by CreateContext's EnsureCreated().
        using (_factory.CreateContext())
        {
        }

        var repository = new TenantProvisioningRepository(BuildOptions());
        var tenantId = Guid.NewGuid();
        var (tenant, settings, branding, membership, provisionedEvent) = BuildGraph(tenantId);

        await repository.ProvisionAsync(tenant, settings, branding, membership, provisionedEvent);

        using var verifyContext = _factory.CreateContext(StaticTenantContext.Bypass);
        Assert.NotNull(await verifyContext.Tenants.SingleOrDefaultAsync(t => t.Id == tenantId));
        Assert.NotNull(await verifyContext.TenantSettings.SingleOrDefaultAsync(s => s.TenantId == tenantId));
        Assert.NotNull(await verifyContext.TenantBrandings.SingleOrDefaultAsync(b => b.TenantId == tenantId));
        Assert.NotNull(await verifyContext.TenantMemberships.SingleOrDefaultAsync(m => m.TenantId == tenantId));

        var outboxRow = await verifyContext.OutboxMessages.SingleOrDefaultAsync(o => o.Id == provisionedEvent.Id);
        Assert.NotNull(outboxRow);
        Assert.Equal(tenantId, outboxRow!.TenantId);
        Assert.Equal(nameof(TenantProvisioned), outboxRow.EventTypeName);
        Assert.Null(outboxRow.ProcessedOnUtc);
    }

    [Fact]
    public async Task ProvisionAsync_WhenTheInsertFails_CommitsNothingAtAll()
    {
        var tenantId = Guid.NewGuid();
        using (var seedContext = _factory.CreateContext(StaticTenantContext.Bypass))
        {
            // Pre-seed a Tenant row with the SAME Id the operation below will try to insert — forces a
            // primary-key violation inside the batched SaveChangesAsync, after the other three entities
            // and the outbox row are already staged. Proves the whole write is one unit: either every
            // row lands, or none does, including the outbox row.
            seedContext.Tenants.Add(new Tenant { Id = tenantId, TenantId = tenantId, Slug = "existing", Name = "Existing" });
            await seedContext.SaveChangesAsync();
        }

        var repository = new TenantProvisioningRepository(BuildOptions());
        var (tenant, settings, branding, membership, provisionedEvent) = BuildGraph(tenantId);

        await Assert.ThrowsAnyAsync<DbUpdateException>(() =>
            repository.ProvisionAsync(tenant, settings, branding, membership, provisionedEvent));

        using var verifyContext = _factory.CreateContext(StaticTenantContext.Bypass);
        Assert.Equal(0, await verifyContext.TenantSettings.CountAsync(s => s.TenantId == tenantId));
        Assert.Equal(0, await verifyContext.TenantBrandings.CountAsync(b => b.TenantId == tenantId));
        Assert.Equal(0, await verifyContext.TenantMemberships.CountAsync(m => m.TenantId == tenantId));
        Assert.Equal(0, await verifyContext.OutboxMessages.CountAsync(o => o.Id == provisionedEvent.Id));
    }

    private static (Tenant Tenant, TenantSettings Settings, TenantBranding Branding, TenantMembership Membership, TenantProvisioned Event) BuildGraph(Guid tenantId)
    {
        var tenant = new Tenant { Id = tenantId, TenantId = tenantId, Slug = "acme", Name = "Acme" };
        var settings = new TenantSettings { TenantId = tenantId };
        var branding = new TenantBranding { TenantId = tenantId };
        var membership = new TenantMembership { Id = Guid.NewGuid(), TenantId = tenantId, UserId = Guid.NewGuid(), Role = TenantRole.Owner };
        var provisionedEvent = new TenantProvisioned(
            Id: Guid.NewGuid(), TenantId: tenantId, CorrelationId: Guid.NewGuid(), CausationId: null,
            ActorId: membership.UserId, Slug: "acme", TenantName: "Acme");

        return (tenant, settings, branding, membership, provisionedEvent);
    }

    private DbContextOptions<AccessDbContext> BuildOptions() =>
        new DbContextOptionsBuilder<AccessDbContext>().UseSqlite(_factory.Connection).Options;

    public void Dispose() => _factory.Dispose();
}
