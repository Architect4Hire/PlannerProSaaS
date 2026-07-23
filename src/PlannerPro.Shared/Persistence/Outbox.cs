using System.Text.Json;
using PlannerPro.Contracts;

namespace PlannerPro.Shared.Persistence;

public sealed class Outbox<TContext>(TContext dbContext) : IOutbox where TContext : SharedDbContext
{
    public Task EnqueueAsync(IIntegrationEvent @event, CancellationToken ct = default)
    {
        var message = new OutboxMessage
        {
            Id = @event.Id,
            TenantId = @event.TenantId,
            // Assembly-qualified so a future consumer-side deserializer could reflectively resolve
            // the exact event type if ever needed; EventTypeName below — not this — is what
            // OutboxDispatcher sends as ServiceBusMessage.Subject.
            Type = @event.GetType().AssemblyQualifiedName!,
            EventTypeName = @event.GetType().Name,
            Content = JsonSerializer.Serialize(@event, @event.GetType()),
            CorrelationId = @event.CorrelationId,
            CausationId = @event.CausationId,
            ActorId = @event.ActorId,
            OccurredOnUtc = DateTime.UtcNow,
        };

        dbContext.OutboxMessages.Add(message);
        return Task.CompletedTask;
    }
}
