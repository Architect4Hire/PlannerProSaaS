using PlannerPro.Contracts;
using PlannerPro.Portfolio.Core.Business;
using PlannerPro.Portfolio.Core.Data;

namespace PlannerPro.Portfolio.Tests.Business;

public sealed class TenantProvisionedBusinessTests
{
    [Fact]
    public async Task HandleAsync_DelegatesToTheDataLayerWithTheSameEvent()
    {
        var dataLayer = new RecordingTenantProvisionedDataLayer();
        var business = new TenantProvisionedBusiness(dataLayer);
        var provisionedEvent = new TenantProvisioned(
            Id: Guid.NewGuid(), TenantId: Guid.NewGuid(), CorrelationId: Guid.NewGuid(), CausationId: null,
            ActorId: Guid.NewGuid(), Slug: "acme", TenantName: "Acme");

        await business.HandleAsync(provisionedEvent);

        Assert.Equal(provisionedEvent, dataLayer.Captured);
    }

    private sealed class RecordingTenantProvisionedDataLayer : ITenantProvisionedDataLayer
    {
        public TenantProvisioned? Captured { get; private set; }

        public Task ProvisionInternalClientAsync(TenantProvisioned provisionedEvent, CancellationToken ct = default)
        {
            Captured = provisionedEvent;
            return Task.CompletedTask;
        }
    }
}
