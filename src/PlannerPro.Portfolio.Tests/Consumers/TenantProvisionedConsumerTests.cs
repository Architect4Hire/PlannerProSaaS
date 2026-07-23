using PlannerPro.Contracts;
using PlannerPro.Portfolio.Consumers;
using PlannerPro.Portfolio.Core.Facade;

namespace PlannerPro.Portfolio.Tests.Consumers;

/// <summary>Confirms the consumer is pure delegation — no tenancy code at all, per ADR-0009. Tenant
/// scoping itself is <c>ServiceBusProcessorHostTests</c>'s job (Shared.Tests), not this consumer's.</summary>
public sealed class TenantProvisionedConsumerTests
{
    [Fact]
    public async Task HandleAsync_DelegatesToTheFacadeWithTheSameEvent()
    {
        var facade = new RecordingClientFacade();
        var consumer = new TenantProvisionedConsumer(facade);
        var provisionedEvent = new TenantProvisioned(
            Id: Guid.NewGuid(), TenantId: Guid.NewGuid(), CorrelationId: Guid.NewGuid(), CausationId: null,
            ActorId: Guid.NewGuid(), Slug: "acme", TenantName: "Acme");

        await consumer.HandleAsync(provisionedEvent);

        Assert.Equal(provisionedEvent, facade.Captured);
    }

    private sealed class RecordingClientFacade : IClientFacade
    {
        public TenantProvisioned? Captured { get; private set; }

        public Task ProvisionInternalClientAsync(TenantProvisioned provisionedEvent, CancellationToken ct = default)
        {
            Captured = provisionedEvent;
            return Task.CompletedTask;
        }
    }
}
