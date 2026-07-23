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
        await using var context = _factory.CreateContext();
        var inbox = new Inbox<TestDbContext>(context);

        Assert.False(await inbox.IsHandledAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task MarkHandledAsync_StagesButDoesNotPersistUntilSaved()
    {
        await using var context = _factory.CreateContext();
        var inbox = new Inbox<TestDbContext>(context);
        var messageId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();

        await inbox.MarkHandledAsync(messageId, tenantId, "TestEvent");

        // Staged but not yet committed.
        Assert.Equal(0, await context.InboxMessages.CountAsync());

        await context.SaveChangesAsync();

        Assert.True(await inbox.IsHandledAsync(messageId));
        var persisted = await context.InboxMessages.SingleAsync();
        Assert.Equal(messageId, persisted.Id);
        Assert.Equal(tenantId, persisted.TenantId);
        Assert.Equal("TestEvent", persisted.EventType);
    }

    public void Dispose() => _factory.Dispose();
}
