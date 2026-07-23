using PlannerPro.Contracts;
using PlannerPro.Portfolio.Core.Data;

namespace PlannerPro.Portfolio.Tests.Data;

public sealed class TenantProvisionedDataLayerTests
{
    [Fact]
    public async Task ProvisionInternalClientAsync_DelegatesToTheRepositoryWithTheSameEvent()
    {
        var repository = new RecordingClientRepository();
        var dataLayer = new TenantProvisionedDataLayer(repository);
        var provisionedEvent = new TenantProvisioned(
            Id: Guid.NewGuid(), TenantId: Guid.NewGuid(), CorrelationId: Guid.NewGuid(), CausationId: null,
            ActorId: Guid.NewGuid(), Slug: "acme", TenantName: "Acme");

        await dataLayer.ProvisionInternalClientAsync(provisionedEvent);

        Assert.Equal(provisionedEvent, repository.Captured);
    }

    private sealed class RecordingClientRepository : IClientRepository
    {
        public TenantProvisioned? Captured { get; private set; }

        public Task ProvisionInternalClientAsync(TenantProvisioned provisionedEvent, CancellationToken ct = default)
        {
            Captured = provisionedEvent;
            return Task.CompletedTask;
        }
    }
}
