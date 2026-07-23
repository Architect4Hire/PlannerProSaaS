using Microsoft.EntityFrameworkCore;
using PlannerPro.Shared.Persistence;
using PlannerPro.Shared.Tests.TestSupport;

namespace PlannerPro.Shared.Tests.Persistence;

public sealed class OutboxTests : IDisposable
{
    private readonly SqliteTestDbContextFactory _factory = new();

    [Fact]
    public async Task EnqueueAsync_StagesAndPersistsARowMatchingTheEvent()
    {
        await using var context = _factory.CreateContext();
        var outbox = new Outbox<TestDbContext>(context);
        var @event = new TestEvent(
            Id: Guid.NewGuid(),
            TenantId: Guid.NewGuid(),
            CorrelationId: Guid.NewGuid(),
            CausationId: Guid.NewGuid(),
            ActorId: Guid.NewGuid(),
            Payload: "hello");

        await outbox.EnqueueAsync(@event);

        // Staged but not yet committed.
        Assert.Equal(0, await context.OutboxMessages.CountAsync());

        await context.SaveChangesAsync();

        var persisted = await context.OutboxMessages.SingleAsync();
        Assert.Equal(@event.Id, persisted.Id);
        Assert.Equal(@event.TenantId, persisted.TenantId);
        Assert.Equal(@event.CorrelationId, persisted.CorrelationId);
        Assert.Equal(@event.CausationId, persisted.CausationId);
        Assert.Equal(@event.ActorId, persisted.ActorId);
        Assert.Equal(typeof(TestEvent).AssemblyQualifiedName, persisted.Type);
        Assert.Equal(nameof(TestEvent), persisted.EventTypeName);
        Assert.Contains("hello", persisted.Content);
        Assert.Null(persisted.ProcessedOnUtc);
    }

    public void Dispose() => _factory.Dispose();
}
