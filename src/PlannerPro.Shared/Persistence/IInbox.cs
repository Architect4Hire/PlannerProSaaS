namespace PlannerPro.Shared.Persistence;

public interface IInbox
{
    Task<bool> IsHandledAsync(Guid messageId, CancellationToken ct = default);

    Task MarkHandledAsync(Guid messageId, Guid tenantId, string eventType, CancellationToken ct = default);
}
