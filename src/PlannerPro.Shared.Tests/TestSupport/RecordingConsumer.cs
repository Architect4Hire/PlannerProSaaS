using Microsoft.EntityFrameworkCore;
using PlannerPro.Shared.Messaging;
using PlannerPro.Shared.Persistence;
using PlannerPro.Shared.Tenancy;

namespace PlannerPro.Shared.Tests.TestSupport;

/// <summary>
/// A minimal consumer following the documented inbox pattern (messaging.md: dedupe on message id in
/// the same transaction as the side effect) plus recording, for assertions, what tenant scope and
/// what rows it saw — proving both idempotency and tenant isolation end to end through
/// <see cref="ServiceBusProcessorHost"/>, not just in isolation.
/// </summary>
internal sealed class RecordingConsumer(TestDbContext context, IInbox inbox, ITenantContext tenant, TestConsumerState state)
    : IIntegrationEventConsumer<TestEvent>
{
    public async Task HandleAsync(TestEvent @event, CancellationToken ct = default)
    {
        if (await inbox.IsHandledAsync(@event.Id, ct)) return;

        state.HandledCount++;
        state.ObservedTenantIds.Add(tenant.TenantId);
        state.ObservedTenantScopedRowCounts.Add(await context.TenantScopedEntities.CountAsync(ct));

        await inbox.MarkHandledAsync(@event.Id, nameof(TestEvent), ct);
        await context.SaveChangesAsync(ct);
    }
}
