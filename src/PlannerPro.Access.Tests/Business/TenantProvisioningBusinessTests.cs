using FluentValidation;
using PlannerPro.Access.Core.Business;
using PlannerPro.Access.Core.Managers.Models.Domain;
using PlannerPro.Access.Core.Managers.Models.ViewModels;
using PlannerPro.Access.Tests.TestSupport;

namespace PlannerPro.Access.Tests.Business;

public sealed class TenantProvisioningBusinessTests : IDisposable
{
    private readonly IdentityTestHarness _harness = new();

    [Fact]
    public async Task ProvisionAsync_NewSlugAndEmail_BuildsFullEnvelopeAndPersistsAtomically()
    {
        var tenantRepository = new StubTenantRepository();
        var dataLayer = new RecordingTenantProvisioningDataLayer();
        var business = new TenantProvisioningBusiness(_harness.UserManager, tenantRepository, dataLayer);
        var correlationId = Guid.NewGuid();
        var viewModel = new SignupViewModel("acme", "Acme Inc", "owner@acme.test", "Correct-Horse-1");

        var result = await business.ProvisionAsync(viewModel, correlationId);

        Assert.Equal("acme", result.Slug);
        Assert.Equal("owner@acme.test", result.OwnerEmail);
        Assert.NotEqual(Guid.Empty, result.TenantId);
        Assert.NotEqual(Guid.Empty, result.OwnerUserId);

        Assert.True(dataLayer.WasCalled);
        Assert.Equal(result.TenantId, dataLayer.Tenant!.Id);
        Assert.Equal(result.TenantId, dataLayer.Tenant.TenantId);
        Assert.Equal("acme", dataLayer.Tenant.Slug);
        Assert.Equal(result.TenantId, dataLayer.Settings!.TenantId);
        Assert.Equal(result.TenantId, dataLayer.Branding!.TenantId);
        Assert.Equal(result.TenantId, dataLayer.OwnerMembership!.TenantId);
        Assert.Equal(result.OwnerUserId, dataLayer.OwnerMembership.UserId);
        Assert.Equal(TenantRole.Owner, dataLayer.OwnerMembership.Role);

        var @event = dataLayer.ProvisionedEvent!;
        Assert.Equal(result.TenantId, @event.TenantId);
        Assert.Equal(correlationId, @event.CorrelationId);
        Assert.Null(@event.CausationId);
        Assert.Equal(result.OwnerUserId, @event.ActorId);
        Assert.Equal("acme", @event.Slug);
        Assert.Equal("Acme Inc", @event.TenantName);
    }

    [Fact]
    public async Task ProvisionAsync_SlugAlreadyTaken_ThrowsValidationExceptionAndNeverTouchesIdentityOrDataLayer()
    {
        var tenantId = Guid.NewGuid();
        var tenantRepository = new StubTenantRepository
        {
            Tenant = new Tenant { Id = tenantId, TenantId = tenantId, Slug = "acme", Name = "Acme" },
        };
        var dataLayer = new RecordingTenantProvisioningDataLayer();
        var business = new TenantProvisioningBusiness(_harness.UserManager, tenantRepository, dataLayer);
        var viewModel = new SignupViewModel("acme", "Acme Inc", "owner@acme.test", "Correct-Horse-1");

        var ex = await Assert.ThrowsAsync<ValidationException>(() => business.ProvisionAsync(viewModel, Guid.NewGuid()));

        Assert.Contains(ex.Errors, failure => failure.PropertyName == nameof(SignupViewModel.Slug));
        Assert.Null(await _harness.UserManager.FindByEmailAsync("owner@acme.test"));
        Assert.False(dataLayer.WasCalled);
    }

    [Fact]
    public async Task ProvisionAsync_EmailAlreadyRegistered_ThrowsValidationExceptionAndWritesNoTenant()
    {
        var existingUser = new ApplicationUser { UserName = "owner@acme.test", Email = "owner@acme.test" };
        await _harness.UserManager.CreateAsync(existingUser, "Correct-Horse-1");

        var tenantRepository = new StubTenantRepository();
        var dataLayer = new RecordingTenantProvisioningDataLayer();
        var business = new TenantProvisioningBusiness(_harness.UserManager, tenantRepository, dataLayer);
        var viewModel = new SignupViewModel("acme", "Acme Inc", "owner@acme.test", "Different-Horse-2");

        await Assert.ThrowsAsync<ValidationException>(() => business.ProvisionAsync(viewModel, Guid.NewGuid()));

        Assert.False(dataLayer.WasCalled);
    }

    public void Dispose() => _harness.Dispose();
}
