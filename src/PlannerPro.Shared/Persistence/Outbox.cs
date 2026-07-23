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
            // Assembly-qualified so a future dispatcher can reflectively deserialize Content back
            // to the exact event type; not the same as the short event-type name messaging.md
            // describes for the eventual ServiceBusMessage.Subject.
            Type = @event.GetType().AssemblyQualifiedName!,
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
