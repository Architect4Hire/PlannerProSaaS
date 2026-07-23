using Microsoft.Extensions.DependencyInjection;
using PlannerPro.Contracts;

namespace PlannerPro.Shared.Messaging;

internal sealed class IntegrationEventRegistration<TEvent> : IIntegrationEventRegistration
    where TEvent : class, IIntegrationEvent
{
    public string EventTypeName { get; } = typeof(TEvent).Name;

    public async Task DispatchAsync(IServiceProvider scopedProvider, BinaryData body, CancellationToken ct)
    {
        var @event = body.ToObjectFromJson<TEvent>()
            ?? throw new InvalidOperationException($"Message body deserialized to null for event type '{EventTypeName}'.");

        var consumer = scopedProvider.GetRequiredService<IIntegrationEventConsumer<TEvent>>();
        await consumer.HandleAsync(@event, ct);
    }
}
