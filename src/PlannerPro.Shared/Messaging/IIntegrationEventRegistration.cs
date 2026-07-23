namespace PlannerPro.Shared.Messaging;

/// <summary>
/// One registered event type's dispatch path — deserialize the message body to the concrete event
/// type and invoke its <see cref="IIntegrationEventConsumer{TEvent}"/>. Non-generic so many of these
/// (one per <c>TEvent</c>) can sit in one <see cref="IntegrationEventConsumerRegistry"/> dictionary;
/// the generic work happens in <see cref="IntegrationEventRegistration{TEvent}"/>.
/// </summary>
internal interface IIntegrationEventRegistration
{
    /// <summary>The event type's short CLR name — matches <see cref="ServiceBusProcessorHost"/>'s
    /// <c>Subject</c> lookup, which in turn matches what <see cref="OutboxDispatcher{TContext}"/>
    /// wrote.</summary>
    string EventTypeName { get; }

    Task DispatchAsync(IServiceProvider scopedProvider, BinaryData body, CancellationToken ct);
}
