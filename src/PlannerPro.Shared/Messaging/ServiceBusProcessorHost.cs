using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PlannerPro.Shared.Tenancy;

namespace PlannerPro.Shared.Messaging;

/// <summary>
/// Receives on one topic/subscription and, for every message, establishes <see cref="ITenantContext"/>
/// from the event envelope BEFORE invoking the registered consumer — see the "Scope-establishment
/// sequence" section of the plan this was built from, and <c>.claude/rules/messaging.md</c>. This is
/// the single call site on the bus path that may call <see cref="TenantContext.Resolve"/>; a consumer
/// author outside <c>PlannerPro.Shared</c> has no compiled way to do it themselves. Registered via
/// <see cref="MessagingServiceCollectionExtensions.AddServiceBusProcessorHost"/>.
/// </summary>
internal sealed class ServiceBusProcessorHost(
    ServiceBusClient client,
    IServiceScopeFactory scopeFactory,
    IntegrationEventConsumerRegistry registry,
    ILogger<ServiceBusProcessorHost> logger,
    string topicName,
    string subscriptionName) : BackgroundService
{
    private ServiceBusProcessor? _processor;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _processor = client.CreateProcessor(topicName, subscriptionName, new ServiceBusProcessorOptions
        {
            // Explicit completion only — no ambiguity about what happens when a handler throws.
            AutoCompleteMessages = false,
        });
        _processor.ProcessMessageAsync += ProcessMessageAsync;
        _processor.ProcessErrorAsync += ProcessErrorAsync;

        await _processor.StartProcessingAsync(stoppingToken);

        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            // Expected on shutdown.
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_processor is not null)
        {
            await _processor.StopProcessingAsync(cancellationToken);
            await _processor.DisposeAsync();
        }

        await base.StopAsync(cancellationToken);
    }

    /// <summary>
    /// SDK-facing glue only: run the decision logic in <see cref="HandleAsync"/>, then translate its
    /// result into the actual completion call. An unhandled exception from the consumer (surfaced by
    /// <see cref="HandleAsync"/> throwing) is logged and the message is abandoned — released back to
    /// Service Bus for redelivery, same as any other transient failure, up to the subscription's
    /// <c>MaxDeliveryCount</c> before the broker dead-letters it itself.
    /// </summary>
    private async Task ProcessMessageAsync(ProcessMessageEventArgs args)
    {
        MessageHandlingResult result;
        try
        {
            result = await HandleAsync(args.Message, args.CancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled exception processing message {MessageId}; abandoning for redelivery.",
                args.Message.MessageId);
            await args.AbandonMessageAsync(args.Message, cancellationToken: args.CancellationToken);
            return;
        }

        switch (result.Disposition)
        {
            case MessageDisposition.Complete:
                await args.CompleteMessageAsync(args.Message, args.CancellationToken);
                break;
            case MessageDisposition.DeadLetter:
                await args.DeadLetterMessageAsync(args.Message, result.Reason, result.Description, args.CancellationToken);
                break;
            default:
                throw new NotSupportedException($"Unhandled {nameof(MessageDisposition)} value '{result.Disposition}'.");
        }
    }

    private Task ProcessErrorAsync(ProcessErrorEventArgs args)
    {
        logger.LogError(args.Exception, "Service Bus processor error on {EntityPath} ({ErrorSource}).",
            args.EntityPath, args.ErrorSource);
        return Task.CompletedTask;
    }

    /// <summary>
    /// The scope-establishment sequence: reject a message with no usable tenant id or no registered
    /// consumer, otherwise open a fresh DI scope, resolve tenancy from the envelope, and dispatch.
    /// </summary>
    internal async Task<MessageHandlingResult> HandleAsync(ServiceBusReceivedMessage message, CancellationToken ct)
    {
        if (!TryGetTenantId(message, out var tenantId))
        {
            logger.LogWarning("Message {MessageId} arrived without a usable TenantId application property; dead-lettering.",
                message.MessageId);
            return MessageHandlingResult.DeadLetter(
                MessagingDeadLetterReasons.MissingTenantId,
                "Integration events must carry a valid TenantId application property; this message did not.");
        }

        var subject = message.Subject;
        if (subject is null || !registry.TryGet(subject, out var registration))
        {
            logger.LogWarning("No consumer is registered for Subject '{Subject}' on message {MessageId}; dead-lettering.",
                subject, message.MessageId);
            return MessageHandlingResult.DeadLetter(
                MessagingDeadLetterReasons.UnrecognizedEventType,
                $"No consumer is registered for event type '{subject}'.");
        }

        await using var scope = scopeFactory.CreateAsyncScope();
        var tenantContext = scope.ServiceProvider.GetRequiredService<TenantContext>();
        tenantContext.Resolve(tenantId, slug: null, role: null, plan: null, status: null);

        await registration.DispatchAsync(scope.ServiceProvider, message.Body, ct);

        return MessageHandlingResult.Complete;
    }

    private static bool TryGetTenantId(ServiceBusReceivedMessage message, out Guid tenantId)
    {
        tenantId = Guid.Empty;
        return message.ApplicationProperties.TryGetValue(IntegrationEventPropertyNames.TenantId, out var raw)
            && raw is string s
            && Guid.TryParse(s, out tenantId)
            // Guid.Empty is ITenantContext's own "unresolved" sentinel (see its doc comment) — never
            // a real tenant, so a message that carries it is a producer bug, not a usable scope.
            && tenantId != Guid.Empty;
    }
}
