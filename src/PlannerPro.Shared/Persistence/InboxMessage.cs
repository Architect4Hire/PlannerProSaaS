namespace PlannerPro.Shared.Persistence;

public sealed class InboxMessage
{
    public required Guid Id { get; init; }
    public required Guid TenantId { get; init; }
    public required string EventType { get; init; }
    public required DateTime ProcessedOnUtc { get; init; }
}
