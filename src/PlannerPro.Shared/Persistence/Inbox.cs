using Microsoft.EntityFrameworkCore;
using PlannerPro.Shared.Tenancy;

namespace PlannerPro.Shared.Persistence;

public sealed class Inbox<TContext>(TContext dbContext, ITenantContext tenant) : IInbox where TContext : SharedDbContext
{
    public Task<bool> IsHandledAsync(Guid messageId, CancellationToken ct = default) =>
        dbContext.InboxMessages.AnyAsync(m => m.Id == messageId, ct);

    public Task MarkHandledAsync(Guid messageId, string eventType, CancellationToken ct = default)
    {
        dbContext.InboxMessages.Add(new InboxMessage
        {
            Id = messageId,
            TenantId = tenant.TenantId,
            EventType = eventType,
            ProcessedOnUtc = DateTime.UtcNow,
        });

        return Task.CompletedTask;
    }
}
