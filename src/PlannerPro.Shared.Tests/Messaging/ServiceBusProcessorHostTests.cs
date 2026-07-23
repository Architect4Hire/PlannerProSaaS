using System.Text.Json;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using PlannerPro.Shared.Messaging;
using PlannerPro.Shared.Tests.TestSupport;

namespace PlannerPro.Shared.Tests.Messaging;

public sealed class ServiceBusProcessorHostTests : IDisposable
{
    private readonly SqliteTestDbContextFactory _factory = new();

    [Fact]
    public async Task HandleAsync_MissingTenantId_DeadLettersWithoutInvokingConsumer()
    {
        var (host, state) = BuildHost();
        var message = ServiceBusModelFactory.ServiceBusReceivedMessage(
            body: EventBody(Guid.NewGuid(), Guid.NewGuid()),
            messageId: Guid.NewGuid().ToString(),
            subject: nameof(TestEvent),
            properties: new Dictionary<string, object>()); // no TenantId

        var result = await host.HandleAsync(message, CancellationToken.None);

        Assert.Equal(MessageDisposition.DeadLetter, result.Disposition);
        Assert.Equal(MessagingDeadLetterReasons.MissingTenantId, result.Reason);
        Assert.Equal(0, state.HandledCount);
    }

    [Fact]
    public async Task HandleAsync_UnparsableTenantId_DeadLettersWithoutInvokingConsumer()
    {
        var (host, state) = BuildHost();
        var message = ServiceBusModelFactory.ServiceBusReceivedMessage(
            body: EventBody(Guid.NewGuid(), Guid.NewGuid()),
            messageId: Guid.NewGuid().ToString(),
            subject: nameof(TestEvent),
            properties: new Dictionary<string, object> { [IntegrationEventPropertyNames.TenantId] = "not-a-guid" });

        var result = await host.HandleAsync(message, CancellationToken.None);

        Assert.Equal(MessageDisposition.DeadLetter, result.Disposition);
        Assert.Equal(MessagingDeadLetterReasons.MissingTenantId, result.Reason);
        Assert.Equal(0, state.HandledCount);
    }

    [Fact]
    public async Task HandleAsync_EmptyGuidTenantId_DeadLettersWithoutInvokingConsumer()
    {
        var (host, state) = BuildHost();
        var message = ServiceBusModelFactory.ServiceBusReceivedMessage(
            body: EventBody(Guid.NewGuid(), Guid.NewGuid()),
            messageId: Guid.NewGuid().ToString(),
            subject: nameof(TestEvent),
            properties: new Dictionary<string, object> { [IntegrationEventPropertyNames.TenantId] = Guid.Empty.ToString() });

        var result = await host.HandleAsync(message, CancellationToken.None);

        Assert.Equal(MessageDisposition.DeadLetter, result.Disposition);
        Assert.Equal(MessagingDeadLetterReasons.MissingTenantId, result.Reason);
        Assert.Equal(0, state.HandledCount);
    }

    [Fact]
    public async Task HandleAsync_UnrecognizedSubject_DeadLetters()
    {
        var (host, _) = BuildHost();
        var message = ServiceBusModelFactory.ServiceBusReceivedMessage(
            body: EventBody(Guid.NewGuid(), Guid.NewGuid()),
            messageId: Guid.NewGuid().ToString(),
            subject: "SomeOtherEvent",
            properties: new Dictionary<string, object> { [IntegrationEventPropertyNames.TenantId] = Guid.NewGuid().ToString() });

        var result = await host.HandleAsync(message, CancellationToken.None);

        Assert.Equal(MessageDisposition.DeadLetter, result.Disposition);
        Assert.Equal(MessagingDeadLetterReasons.UnrecognizedEventType, result.Reason);
    }

    [Fact]
    public async Task HandleAsync_KnownEvent_CompletesAndEstablishesTenantFromTheEnvelope()
    {
        var (host, state) = BuildHost();
        var tenantId = Guid.NewGuid();
        var message = NewTestEventMessage(tenantId, Guid.NewGuid());

        var result = await host.HandleAsync(message, CancellationToken.None);

        Assert.Equal(MessageDisposition.Complete, result.Disposition);
        Assert.Equal(1, state.HandledCount);
        Assert.Equal(tenantId, Assert.Single(state.ObservedTenantIds));
    }

    [Fact]
    public async Task HandleAsync_EstablishesTenantFromEnvelope_ConsumerCannotSeeAnotherTenantsRows()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        await using (var seedContext = _factory.CreateContext(StaticTenantContext.For(tenantA)))
        {
            seedContext.TenantScopedEntities.Add(new TenantScopedEntity { Id = Guid.NewGuid(), Name = "a-row" });
            await seedContext.SaveChangesAsync();
        }

        var (host, state) = BuildHost();
        var message = NewTestEventMessage(tenantB, Guid.NewGuid());

        var result = await host.HandleAsync(message, CancellationToken.None);

        Assert.Equal(MessageDisposition.Complete, result.Disposition);
        Assert.Equal(0, Assert.Single(state.ObservedTenantScopedRowCounts));
    }

    [Fact]
    public async Task HandleAsync_RedeliveredMessage_ConsumerAppliesTheEffectOnlyOnce()
    {
        var (host, state) = BuildHost();
        var eventId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();

        var first = await host.HandleAsync(NewTestEventMessage(tenantId, eventId), CancellationToken.None);
        var redelivered = await host.HandleAsync(NewTestEventMessage(tenantId, eventId), CancellationToken.None);

        Assert.Equal(MessageDisposition.Complete, first.Disposition);
        Assert.Equal(MessageDisposition.Complete, redelivered.Disposition);
        Assert.Equal(1, state.HandledCount);
    }

    private static ServiceBusReceivedMessage NewTestEventMessage(Guid tenantId, Guid eventId) =>
        ServiceBusModelFactory.ServiceBusReceivedMessage(
            body: EventBody(tenantId, eventId),
            messageId: eventId.ToString(),
            subject: nameof(TestEvent),
            properties: new Dictionary<string, object> { [IntegrationEventPropertyNames.TenantId] = tenantId.ToString() });

    private static BinaryData EventBody(Guid tenantId, Guid eventId) => BinaryData.FromObjectAsJson(new TestEvent(
        Id: eventId,
        TenantId: tenantId,
        CorrelationId: Guid.NewGuid(),
        CausationId: null,
        ActorId: Guid.NewGuid(),
        Payload: "hello"));

    private (ServiceBusProcessorHost Host, TestConsumerState State) BuildHost()
    {
        var services = new ServiceCollection();
        _factory.ConfigureServices(services);
        services.AddSingleton<TestConsumerState>();
        services.AddIntegrationEventConsumer<TestEvent, RecordingConsumer>();

        var provider = services.BuildServiceProvider();
        var registry = provider.GetRequiredService<IntegrationEventConsumerRegistry>();
        var state = provider.GetRequiredService<TestConsumerState>();

        // ServiceBusClient is only used by ExecuteAsync (to create the processor) — HandleAsync never
        // touches it, so this test never spins up a real/emulated Service Bus connection.
        var host = new ServiceBusProcessorHost(
            client: null!,
            provider.GetRequiredService<IServiceScopeFactory>(),
            registry,
            NullLogger<ServiceBusProcessorHost>.Instance,
            topicName: "test-topic",
            subscriptionName: "test-subscription");

        return (host, state);
    }

    public void Dispose() => _factory.Dispose();
}
