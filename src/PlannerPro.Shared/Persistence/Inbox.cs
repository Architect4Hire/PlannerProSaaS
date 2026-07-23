using Microsoft.EntityFrameworkCore;

namespace PlannerPro.Shared.Persistence;

public sealed class Inbox<TContext>(TContext dbContext) : IInbox where TContext : SharedDbContext
{
    public Task<bool> IsHandledAsync(Guid messageId, CancellationToken ct = default) =>
        dbContext.InboxMessages.AnyAsync(m => m.Id == messageId, ct);

    public Task MarkHandledAsync(Guid messageId, Guid tenantId, string eventType, CancellationToken ct = default)
    {
        dbContext.InboxMessages.Add(new InboxMessage
        {
            Id = messageId,
            TenantId = tenantId,
            EventType = eventType,
            ProcessedOnUtc = DateTime.UtcNow,
        });

        return Task.CompletedTask;
    }
}
