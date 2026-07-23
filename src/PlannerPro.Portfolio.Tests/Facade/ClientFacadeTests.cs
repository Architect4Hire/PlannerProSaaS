using PlannerPro.Contracts;
using PlannerPro.Portfolio.Core.Business;
using PlannerPro.Portfolio.Core.Facade;

namespace PlannerPro.Portfolio.Tests.Facade;

public sealed class ClientFacadeTests
{
    [Fact]
    public async Task ProvisionInternalClientAsync_DelegatesToBusinessWithTheSameEvent()
    {
        var business = new RecordingTenantProvisionedBusiness();
        var facade = new ClientFacade(business);
        var provisionedEvent = new TenantProvisioned(
            Id: Guid.NewGuid(), TenantId: Guid.NewGuid(), CorrelationId: Guid.NewGuid(), CausationId: null,
            ActorId: Guid.NewGuid(), Slug: "acme", TenantName: "Acme");

        await facade.ProvisionInternalClientAsync(provisionedEvent);

        Assert.Equal(provisionedEvent, business.Captured);
    }

    private sealed class RecordingTenantProvisionedBusiness : ITenantProvisionedBusiness
    {
        public TenantProvisioned? Captured { get; private set; }

        public Task HandleAsync(TenantProvisioned provisionedEvent, CancellationToken ct = default)
        {
            Captured = provisionedEvent;
            return Task.CompletedTask;
        }
    }
}
