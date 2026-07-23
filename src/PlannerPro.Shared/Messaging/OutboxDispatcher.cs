using Azure.Messaging.ServiceBus;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PlannerPro.Shared.Persistence;

namespace PlannerPro.Shared.Messaging;

/// <summary>
/// Polls <typeparamref name="TContext"/>'s unprocessed <see cref="OutboxMessage"/> rows oldest-first
/// and sends each as a <see cref="ServiceBusMessage"/> — the only thing in the system allowed to send
/// to Service Bus (see <c>.claude/rules/messaging.md</c>). Registered via
/// <see cref="MessagingServiceCollectionExtensions.AddOutboxDispatcher{TContext}"/>.
/// </summary>
internal sealed class OutboxDispatcher<TContext>(
    ServiceBusSender sender,
    IServiceScopeFactory scopeFactory,
    ILogger<OutboxDispatcher<TContext>> logger,
    TimeSpan pollInterval) : BackgroundService
    where TContext : SharedDbContext
{
    private const int BatchSize = 50;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await DispatchOnceAsync(stoppingToken);
                await Task.Delay(pollInterval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Expected on shutdown; the loop condition above ends it.
            }
            catch (Exception ex)
            {
                // A transient failure here (the database briefly unreachable, etc.) must not take the
                // whole host down — BackgroundService's default is to stop and dispose the host on any
                // unhandled exception from ExecuteAsync. Log loudly and retry next poll instead; the
                // outbox row itself is untouched, so nothing is lost.
                logger.LogError(ex, "Outbox poll failed; retrying in {PollInterval}.", pollInterval);
                await TryDelayAsync(stoppingToken);
            }
        }
    }

    private async Task TryDelayAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(pollInterval, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Expected on shutdown; the loop condition in ExecuteAsync ends it.
        }
    }

    /// <summary>
    /// One poll: send every currently-unprocessed row, oldest first. A row whose send fails is left
    /// with <see cref="OutboxMessage.ProcessedOnUtc"/> still <c>null</c> — logged and skipped, not
    /// retried inline — so one bad or unreachable event doesn't stall the rest of the batch; the next
    /// poll picks it back up.
    /// </summary>
    internal async Task DispatchOnceAsync(CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<TContext>();

        var pending = await context.OutboxMessages
            .Where(m => m.ProcessedOnUtc == null)
            .OrderBy(m => m.OccurredOnUtc)
            .Take(BatchSize)
            .ToListAsync(ct);

        foreach (var message in pending)
        {
            var sbMessage = new ServiceBusMessage(message.Content)
            {
                MessageId = message.Id.ToString(),
                Subject = message.EventTypeName,
            };
            sbMessage.ApplicationProperties[IntegrationEventPropertyNames.TenantId] = message.TenantId.ToString();
            sbMessage.ApplicationProperties[IntegrationEventPropertyNames.CorrelationId] = message.CorrelationId.ToString();
            if (message.CausationId is { } causationId)
                sbMessage.ApplicationProperties[IntegrationEventPropertyNames.CausationId] = causationId.ToString();

            try
            {
                await sender.SendMessageAsync(sbMessage, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                // Shutdown, not a send failure — propagate so ExecuteAsync's loop stops cleanly
                // instead of logging every remaining row in the batch as an error.
                throw;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to send outbox message {MessageId}; left unprocessed for retry.", message.Id);
                continue;
            }

            message.ProcessedOnUtc = DateTime.UtcNow;
        }

        await context.SaveChangesAsync(ct);
    }
}
