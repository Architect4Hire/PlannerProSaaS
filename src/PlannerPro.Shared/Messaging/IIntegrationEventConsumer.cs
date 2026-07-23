using PlannerPro.Contracts;

namespace PlannerPro.Shared.Messaging;

public interface IIntegrationEventConsumer<TEvent> where TEvent : IIntegrationEvent
{
    Task HandleAsync(TEvent @event, CancellationToken ct = default);
}
