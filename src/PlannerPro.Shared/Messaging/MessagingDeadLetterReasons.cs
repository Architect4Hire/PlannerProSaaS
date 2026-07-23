namespace PlannerPro.Shared.Messaging;

/// <summary>
/// Dead-letter reason codes <see cref="ServiceBusProcessorHost"/> uses for messages it refuses to
/// process under an ambiguous or unknown scope, rather than risk running a consumer incorrectly.
/// </summary>
internal static class MessagingDeadLetterReasons
{
    /// <summary>
    /// No consumer has any way to establish tenant scope for this message — see
    /// <c>.claude/rules/tenancy.md</c> and <c>.claude/rules/messaging.md</c>.
    /// </summary>
    public const string MissingTenantId = "MissingTenantId";

    /// <summary>
    /// <see cref="Azure.Messaging.ServiceBus.ServiceBusReceivedMessage.Subject"/> doesn't match any
    /// event type registered via <c>AddIntegrationEventConsumer</c> — a subscription filter or
    /// registration mismatch, not something retrying will fix.
    /// </summary>
    public const string UnrecognizedEventType = "UnrecognizedEventType";
}
