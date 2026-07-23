using PlannerPro.Contracts;

namespace PlannerPro.Shared.Persistence;

public interface IOutbox
{
    Task EnqueueAsync(IIntegrationEvent @event, CancellationToken ct = default);
}
