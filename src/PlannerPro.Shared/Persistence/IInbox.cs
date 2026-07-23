namespace PlannerPro.Shared.Persistence;

public interface IInbox
{
    Task<bool> IsHandledAsync(Guid messageId, CancellationToken ct = default);

    /// <summary>
    /// Records <paramref name="messageId"/> as handled. Deliberately takes no tenant id — the
    /// implementation sources it from the ambient <see cref="Tenancy.ITenantContext"/> (established
    /// from the event envelope by <see cref="Messaging.ServiceBusProcessorHost"/>), so a consumer
    /// author has no parameter here they could accidentally source from the message body instead.
    /// </summary>
    Task MarkHandledAsync(Guid messageId, string eventType, CancellationToken ct = default);
}
