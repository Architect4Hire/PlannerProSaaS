namespace PlannerPro.Shared.Messaging;

/// <summary>
/// Every event type this service can consume, keyed by <see
/// cref="IIntegrationEventRegistration.EventTypeName"/>. Built once, as a singleton, from every
/// <c>AddIntegrationEventConsumer&lt;TEvent, TConsumer&gt;()</c> call made during startup — the
/// standard "many small DI registrations feed one aggregating singleton" pattern, so registration
/// order across calls doesn't matter.
/// </summary>
internal sealed class IntegrationEventConsumerRegistry
{
    private readonly IReadOnlyDictionary<string, IIntegrationEventRegistration> _byEventTypeName;

    public IntegrationEventConsumerRegistry(IEnumerable<IIntegrationEventRegistration> registrations)
    {
        _byEventTypeName = registrations.ToDictionary(r => r.EventTypeName);
    }

    public bool TryGet(string eventTypeName, out IIntegrationEventRegistration registration) =>
        _byEventTypeName.TryGetValue(eventTypeName, out registration!);
}
