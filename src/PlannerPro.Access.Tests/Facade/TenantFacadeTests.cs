using FluentValidation;
using PlannerPro.Access.Core.Business;
using PlannerPro.Access.Core.Facade;
using PlannerPro.Access.Core.Managers.Models.ServiceModels;
using PlannerPro.Access.Core.Managers.Models.ViewModels;
using PlannerPro.Access.Core.Managers.Validators;

namespace PlannerPro.Access.Tests.Facade;

public sealed class TenantFacadeTests
{
    [Fact]
    public async Task ProvisionAsync_InvalidViewModel_ThrowsValidationExceptionRatherThanCallingBusiness()
    {
        var expected = new TenantProvisionedServiceModel(Guid.NewGuid(), "acme", Guid.NewGuid(), "owner@acme.test");
        var business = new RecordingTenantProvisioningBusiness(expected);
        var facade = new TenantFacade(new SignupViewModelValidator(), business);

        await Assert.ThrowsAsync<ValidationException>(() =>
            facade.ProvisionAsync(new SignupViewModel("AB", "", "not-an-email", ""), Guid.NewGuid()));

        Assert.False(business.WasCalled);
    }

    [Fact]
    public async Task ProvisionAsync_ValidViewModel_DelegatesToBusiness()
    {
        var expected = new TenantProvisionedServiceModel(Guid.NewGuid(), "acme", Guid.NewGuid(), "owner@acme.test");
        var business = new RecordingTenantProvisioningBusiness(expected);
        var facade = new TenantFacade(new SignupViewModelValidator(), business);
        var correlationId = Guid.NewGuid();

        var result = await facade.ProvisionAsync(new SignupViewModel("acme", "Acme Inc", "owner@acme.test", "Correct-Horse-1"), correlationId);

        Assert.Equal(expected, result);
        Assert.True(business.WasCalled);
        Assert.Equal(correlationId, business.CapturedCorrelationId);
    }

    private sealed class RecordingTenantProvisioningBusiness(TenantProvisionedServiceModel result) : ITenantProvisioningBusiness
    {
        public bool WasCalled { get; private set; }

        public Guid CapturedCorrelationId { get; private set; }

        public Task<TenantProvisionedServiceModel> ProvisionAsync(SignupViewModel viewModel, Guid correlationId, CancellationToken ct = default)
        {
            WasCalled = true;
            CapturedCorrelationId = correlationId;
            return Task.FromResult(result);
        }
    }
}
