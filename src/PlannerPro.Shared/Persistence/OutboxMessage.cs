namespace PlannerPro.Shared.Persistence;

public sealed class OutboxMessage
{
    public required Guid Id { get; init; }
    public required Guid TenantId { get; init; }
    public required string Type { get; init; }
    public required string Content { get; init; }
    public required Guid CorrelationId { get; init; }
    public Guid? CausationId { get; init; }
    public required Guid ActorId { get; init; }
    public required DateTime OccurredOnUtc { get; init; }
    public DateTime? ProcessedOnUtc { get; set; }
}
