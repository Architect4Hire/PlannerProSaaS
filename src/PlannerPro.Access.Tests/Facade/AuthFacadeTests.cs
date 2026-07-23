using FluentValidation;
using PlannerPro.Access.Core.Business;
using PlannerPro.Access.Core.Facade;
using PlannerPro.Access.Core.Managers.Models.ServiceModels;
using PlannerPro.Access.Core.Managers.Models.ViewModels;
using PlannerPro.Access.Core.Managers.Validators;

namespace PlannerPro.Access.Tests.Facade;

public sealed class AuthFacadeTests
{
    [Fact]
    public async Task LoginAsync_InvalidViewModel_ThrowsValidationExceptionRatherThanCallingBusiness()
    {
        var business = new RecordingAuthBusiness(new SessionServiceModel(Guid.NewGuid(), "x@example.com", false));
        var facade = new AuthFacade(new LoginViewModelValidator(), business);

        await Assert.ThrowsAsync<ValidationException>(() => facade.LoginAsync(new LoginViewModel("not-an-email", "")));

        Assert.False(business.WasCalled);
    }

    [Fact]
    public async Task LoginAsync_ValidViewModel_DelegatesToBusiness()
    {
        var expected = new SessionServiceModel(Guid.NewGuid(), "owner@acme.test", true);
        var business = new RecordingAuthBusiness(expected);
        var facade = new AuthFacade(new LoginViewModelValidator(), business);

        var result = await facade.LoginAsync(new LoginViewModel("owner@acme.test", "Correct-Horse-1"));

        Assert.Equal(expected, result);
        Assert.True(business.WasCalled);
    }

    private sealed class RecordingAuthBusiness(SessionServiceModel? result) : IAuthBusiness
    {
        public bool WasCalled { get; private set; }

        public Task<SessionServiceModel?> AuthenticateAsync(string email, string password, CancellationToken ct = default)
        {
            WasCalled = true;
            return Task.FromResult(result);
        }
    }
}
