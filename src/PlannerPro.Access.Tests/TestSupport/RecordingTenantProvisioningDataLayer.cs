using PlannerPro.Access.Core.Data;
using PlannerPro.Access.Core.Managers.Models.Domain;
using PlannerPro.Contracts;

namespace PlannerPro.Access.Tests.TestSupport;

internal sealed class RecordingTenantProvisioningDataLayer : ITenantProvisioningDataLayer
{
    public bool WasCalled { get; private set; }

    public Tenant? Tenant { get; private set; }

    public TenantSettings? Settings { get; private set; }

    public TenantBranding? Branding { get; private set; }

    public TenantMembership? OwnerMembership { get; private set; }

    public TenantProvisioned? ProvisionedEvent { get; private set; }

    public Task ProvisionAsync(
        Tenant tenant,
        TenantSettings settings,
        TenantBranding branding,
        TenantMembership ownerMembership,
        TenantProvisioned provisionedEvent,
        CancellationToken ct = default)
    {
        WasCalled = true;
        Tenant = tenant;
        Settings = settings;
        Branding = branding;
        OwnerMembership = ownerMembership;
        ProvisionedEvent = provisionedEvent;
        return Task.CompletedTask;
    }
}
