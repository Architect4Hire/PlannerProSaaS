using Microsoft.EntityFrameworkCore;
using PlannerPro.Shared.Persistence;
using PlannerPro.Shared.Tests.TestSupport;

namespace PlannerPro.Shared.Tests.Persistence;

public sealed class InboxTests : IDisposable
{
    private readonly SqliteTestDbContextFactory _factory = new();

    [Fact]
    public async Task IsHandledAsync_ReturnsFalseForAnUnknownMessageId()
    {
        var tenantId = Guid.NewGuid();
        await using var context = _factory.CreateContext(StaticTenantContext.For(tenantId));
        var inbox = new Inbox<TestDbContext>(context, StaticTenantContext.For(tenantId));

        Assert.False(await inbox.IsHandledAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task MarkHandledAsync_StagesButDoesNotPersistUntilSaved()
    {
        var tenantId = Guid.NewGuid();
        await using var context = _factory.CreateContext(StaticTenantContext.For(tenantId));
        var inbox = new Inbox<TestDbContext>(context, StaticTenantContext.For(tenantId));
        var messageId = Guid.NewGuid();

        await inbox.MarkHandledAsync(messageId, "TestEvent");

        // Staged but not yet committed.
        Assert.Equal(0, await context.InboxMessages.CountAsync());

        await context.SaveChangesAsync();

        Assert.True(await inbox.IsHandledAsync(messageId));
        var persisted = await context.InboxMessages.SingleAsync();
        Assert.Equal(messageId, persisted.Id);
        Assert.Equal(tenantId, persisted.TenantId);
        Assert.Equal("TestEvent", persisted.EventType);
    }

    /// <summary>
    /// The whole point of dropping the tenantId parameter: MarkHandledAsync always stamps from the
    /// ambient ITenantContext, so it's not possible to record a row under a different tenant than the
    /// one the consumer is scoped to.
    /// </summary>
    [Fact]
    public async Task MarkHandledAsync_AlwaysStampsFromTheAmbientTenantContext_NeverACallerSuppliedValue()
    {
        var scopedTenant = Guid.NewGuid();
        await using var context = _factory.CreateContext(StaticTenantContext.For(scopedTenant));
        var inbox = new Inbox<TestDbContext>(context, StaticTenantContext.For(scopedTenant));

        await inbox.MarkHandledAsync(Guid.NewGuid(), "TestEvent");
        await context.SaveChangesAsync();

        var persisted = await context.InboxMessages.SingleAsync();
        Assert.Equal(scopedTenant, persisted.TenantId);
    }

    public void Dispose() => _factory.Dispose();
}
