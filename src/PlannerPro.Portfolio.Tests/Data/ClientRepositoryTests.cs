using Microsoft.EntityFrameworkCore;
using PlannerPro.Contracts;
using PlannerPro.Portfolio.Core.Data;
using PlannerPro.Portfolio.Tests.TestSupport;
using PlannerPro.Shared.Persistence;

namespace PlannerPro.Portfolio.Tests.Data;

public sealed class ClientRepositoryTests : IDisposable
{
    private readonly SqlitePortfolioDbContextFactory _factory = new();

    [Fact]
    public async Task ProvisionInternalClientAsync_NewEvent_CreatesOneInternalClientAndMarksTheInboxRow()
    {
        var tenantId = Guid.NewGuid();
        var provisionedEvent = BuildEvent(tenantId);

        using var context = _factory.CreateContext(StaticTenantContext.For(tenantId));
        var repository = new ClientRepository(context, new Inbox<PortfolioDbContext>(context, StaticTenantContext.For(tenantId)));

        await repository.ProvisionInternalClientAsync(provisionedEvent);

        var client = Assert.Single(context.Clients);
        Assert.Equal(tenantId, client.TenantId);
        Assert.Equal("Internal", client.Name);
        Assert.True(await context.InboxMessages.AnyAsync(m => m.Id == provisionedEvent.Id));
    }

    [Fact]
    public async Task ProvisionInternalClientAsync_RedeliveredEvent_IsANoOp()
    {
        var tenantId = Guid.NewGuid();
        var provisionedEvent = BuildEvent(tenantId);

        using var context = _factory.CreateContext(StaticTenantContext.For(tenantId));
        var repository = new ClientRepository(context, new Inbox<PortfolioDbContext>(context, StaticTenantContext.For(tenantId)));

        await repository.ProvisionInternalClientAsync(provisionedEvent);
        // Same event Id delivered a second time — simulates the dispatcher's at-least-once resend
        // after a crash between send and stamp, per .claude/rules/messaging.md.
        await repository.ProvisionInternalClientAsync(provisionedEvent);

        Assert.Single(context.Clients);
        Assert.Equal(1, await context.InboxMessages.CountAsync(m => m.Id == provisionedEvent.Id));
    }

    [Fact]
    public async Task ProvisionInternalClientAsync_TwoTenants_EachGetsExactlyOneClient()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        using var contextA = _factory.CreateContext(StaticTenantContext.For(tenantA));
        await new ClientRepository(contextA, new Inbox<PortfolioDbContext>(contextA, StaticTenantContext.For(tenantA)))
            .ProvisionInternalClientAsync(BuildEvent(tenantA));

        using var contextB = _factory.CreateContext(StaticTenantContext.For(tenantB));
        await new ClientRepository(contextB, new Inbox<PortfolioDbContext>(contextB, StaticTenantContext.For(tenantB)))
            .ProvisionInternalClientAsync(BuildEvent(tenantB));

        using var verifyContext = _factory.CreateContext(StaticTenantContext.Bypass);
        Assert.Equal(1, await verifyContext.Clients.CountAsync(c => c.TenantId == tenantA));
        Assert.Equal(1, await verifyContext.Clients.CountAsync(c => c.TenantId == tenantB));

        // Tenant B's own (filtered, non-bypass) view must never surface tenant A's row.
        using var scopedAsTenantB = _factory.CreateContext(StaticTenantContext.For(tenantB));
        var visibleToB = await scopedAsTenantB.Clients.ToListAsync();
        Assert.All(visibleToB, c => Assert.Equal(tenantB, c.TenantId));
    }

    private static TenantProvisioned BuildEvent(Guid tenantId) => new(
        Id: Guid.NewGuid(), TenantId: tenantId, CorrelationId: Guid.NewGuid(), CausationId: null,
        ActorId: Guid.NewGuid(), Slug: "acme", TenantName: "Acme");

    public void Dispose() => _factory.Dispose();
}
