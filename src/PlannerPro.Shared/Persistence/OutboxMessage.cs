namespace PlannerPro.Shared.Persistence;

public sealed class OutboxMessage
{
    public required Guid Id { get; init; }
    public required Guid TenantId { get; init; }
    public required string Type { get; init; }

    /// <summary>
    /// The event type's short CLR name, captured at enqueue time — what <c>OutboxDispatcher</c> sends
    /// as <c>ServiceBusMessage.Subject</c>. Deliberately NOT derived by reflecting <see cref="Type"/>
    /// back into a CLR <see cref="System.Type"/> at dispatch time: that reflection can fail (assembly
    /// renamed, version drift between enqueue and a redeployed dispatcher) and would otherwise crash
    /// the whole dispatcher loop for every tenant over one bad row.
    /// </summary>
    public required string EventTypeName { get; init; }

    public required string Content { get; init; }
    public required Guid CorrelationId { get; init; }
    public Guid? CausationId { get; init; }
    public required Guid ActorId { get; init; }
    public required DateTime OccurredOnUtc { get; init; }
    public DateTime? ProcessedOnUtc { get; set; }
}
