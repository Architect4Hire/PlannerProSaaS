using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using PlannerPro.Shared.Messaging;
using PlannerPro.Shared.Persistence;
using PlannerPro.Shared.Tests.TestSupport;

namespace PlannerPro.Shared.Tests.Messaging;

public sealed class OutboxDispatcherTests : IDisposable
{
    private readonly SqliteTestDbContextFactory _factory = new();

    [Fact]
    public async Task DispatchOnceAsync_SendsPendingRowsAndStampsProcessedOnUtc()
    {
        var tenantId = Guid.NewGuid();
        var correlationId = Guid.NewGuid();
        var causationId = Guid.NewGuid();
        var messageId = await SeedOutboxRowAsync(tenantId, correlationId, causationId, occurredOnUtc: DateTime.UtcNow);

        var sender = new FakeServiceBusSender();
        var dispatcher = new OutboxDispatcher<TestDbContext>(
            sender, BuildScopeFactory(), NullLogger<OutboxDispatcher<TestDbContext>>.Instance, TimeSpan.FromMinutes(1));

        await dispatcher.DispatchOnceAsync(CancellationToken.None);

        var sent = Assert.Single(sender.SentMessages);
        Assert.Equal(messageId.ToString(), sent.MessageId);
        Assert.Equal(nameof(TestEvent), sent.Subject);
        Assert.Equal(tenantId.ToString(), (string)sent.ApplicationProperties[IntegrationEventPropertyNames.TenantId]);
        Assert.Equal(correlationId.ToString(), (string)sent.ApplicationProperties[IntegrationEventPropertyNames.CorrelationId]);
        Assert.Equal(causationId.ToString(), (string)sent.ApplicationProperties[IntegrationEventPropertyNames.CausationId]);

        await using var verifyContext = _factory.CreateContext();
        var persisted = await verifyContext.OutboxMessages.SingleAsync(m => m.Id == messageId);
        Assert.NotNull(persisted.ProcessedOnUtc);
    }

    [Fact]
    public async Task DispatchOnceAsync_LeavesAFailedSendUnprocessedForRetry_AndContinuesWithTheRest()
    {
        var olderId = await SeedOutboxRowAsync(Guid.NewGuid(), Guid.NewGuid(), null, occurredOnUtc: DateTime.UtcNow.AddMinutes(-1));
        var newerId = await SeedOutboxRowAsync(Guid.NewGuid(), Guid.NewGuid(), null, occurredOnUtc: DateTime.UtcNow);

        var sender = new FakeServiceBusSender { ThrowOnNextSend = new InvalidOperationException("send failed") };
        var dispatcher = new OutboxDispatcher<TestDbContext>(
            sender, BuildScopeFactory(), NullLogger<OutboxDispatcher<TestDbContext>>.Instance, TimeSpan.FromMinutes(1));

        await dispatcher.DispatchOnceAsync(CancellationToken.None);

        // Oldest row (first in the batch) failed to send; the dispatcher moved on to the next.
        var sent = Assert.Single(sender.SentMessages);
        Assert.Equal(newerId.ToString(), sent.MessageId);

        await using var verifyContext = _factory.CreateContext();
        var older = await verifyContext.OutboxMessages.SingleAsync(m => m.Id == olderId);
        var newer = await verifyContext.OutboxMessages.SingleAsync(m => m.Id == newerId);
        Assert.Null(older.ProcessedOnUtc);
        Assert.NotNull(newer.ProcessedOnUtc);
    }

    [Fact]
    public async Task ExecuteAsync_APollThrowsTransiently_LogsAndRetriesRatherThanStoppingTheHost()
    {
        var messageId = await SeedOutboxRowAsync(Guid.NewGuid(), Guid.NewGuid(), null, DateTime.UtcNow);
        var flakyScopeFactory = new FailOnceThenDelegateScopeFactory(BuildScopeFactory());
        var sender = new FakeServiceBusSender();
        var dispatcher = new OutboxDispatcher<TestDbContext>(
            sender, flakyScopeFactory, NullLogger<OutboxDispatcher<TestDbContext>>.Instance, TimeSpan.FromMilliseconds(20));

        await dispatcher.StartAsync(CancellationToken.None);
        try
        {
            // First poll throws (simulated transient DB failure); the second, ~20ms later, must still
            // run and succeed — proving the failure was caught and logged, not left to fault/stop the
            // whole BackgroundService.
            var deadline = DateTime.UtcNow.AddSeconds(5);
            while (sender.SentMessages.Count == 0 && DateTime.UtcNow < deadline)
            {
                await Task.Delay(20);
            }

            Assert.False(dispatcher.ExecuteTask!.IsFaulted, "A transient poll failure must not fault the background service.");
            var sent = Assert.Single(sender.SentMessages);
            Assert.Equal(messageId.ToString(), sent.MessageId);
        }
        finally
        {
            await dispatcher.StopAsync(CancellationToken.None);
        }
    }

    private sealed class FailOnceThenDelegateScopeFactory(IServiceScopeFactory inner) : IServiceScopeFactory
    {
        private int _calls;

        public IServiceScope CreateScope() =>
            Interlocked.Increment(ref _calls) == 1
                ? throw new InvalidOperationException("Simulated transient failure.")
                : inner.CreateScope();
    }

    private async Task<Guid> SeedOutboxRowAsync(Guid tenantId, Guid correlationId, Guid? causationId, DateTime occurredOnUtc)
    {
        var id = Guid.NewGuid();
        await using var context = _factory.CreateContext();
        context.OutboxMessages.Add(new OutboxMessage
        {
            Id = id,
            TenantId = tenantId,
            Type = typeof(TestEvent).AssemblyQualifiedName!,
            EventTypeName = nameof(TestEvent),
            Content = "{}",
            CorrelationId = correlationId,
            CausationId = causationId,
            ActorId = Guid.NewGuid(),
            OccurredOnUtc = occurredOnUtc,
        });
        await context.SaveChangesAsync();
        return id;
    }

    private IServiceScopeFactory BuildScopeFactory()
    {
        var services = new ServiceCollection();
        services.AddScoped(_ => _factory.CreateContext());
        return services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();
    }

    public void Dispose() => _factory.Dispose();
}
