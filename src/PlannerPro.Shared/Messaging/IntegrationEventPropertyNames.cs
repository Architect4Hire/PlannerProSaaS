namespace PlannerPro.Shared.Messaging;

/// <summary>
/// <see cref="Azure.Messaging.ServiceBus.ServiceBusMessage.ApplicationProperties"/> keys the
/// envelope is promoted to/from. Written by <see cref="OutboxDispatcher{TContext}"/>, read by
/// <see cref="ServiceBusProcessorHost"/> — kept as constants so the two sides can't drift.
/// </summary>
internal static class IntegrationEventPropertyNames
{
    public const string TenantId = "TenantId";
    public const string CorrelationId = "CorrelationId";
    public const string CausationId = "CausationId";
}
