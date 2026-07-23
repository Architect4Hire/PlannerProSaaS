namespace PlannerPro.Shared.Tests.TestSupport;

/// <summary>
/// Registered as a singleton so it survives across the per-message DI scopes
/// <see cref="Messaging.ServiceBusProcessorHost"/> creates — <see cref="RecordingConsumer"/> writes
/// to it; tests read from it.
/// </summary>
internal sealed class TestConsumerState
{
    public int HandledCount { get; set; }
    public List<Guid> ObservedTenantIds { get; } = [];
    public List<int> ObservedTenantScopedRowCounts { get; } = [];
}
