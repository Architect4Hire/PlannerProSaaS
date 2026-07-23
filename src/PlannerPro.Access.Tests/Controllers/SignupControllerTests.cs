using FluentValidation;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using PlannerPro.Access.Controllers;
using PlannerPro.Access.Core.Facade;
using PlannerPro.Access.Core.Managers.Models.ServiceModels;
using PlannerPro.Access.Core.Managers.Models.ViewModels;
using PlannerPro.Shared.Authentication;
using PlannerPro.Shared.Http;

namespace PlannerPro.Access.Tests.Controllers;

/// <summary>
/// Exercises the controller against a recording <see cref="ITenantFacade"/> fake — the facade/business
/// chain itself is covered by <c>TenantFacadeTests</c>/<c>TenantProvisioningBusinessTests</c>. Needs a
/// real (if minimal) cookie authentication scheme registered on <see cref="HttpContext.RequestServices"/>
/// because the action really calls <see cref="HttpContext.SignInAsync(string, System.Security.Claims.ClaimsPrincipal)"/>
/// on success, same as <see cref="AuthController"/>'s login.
/// </summary>
public sealed class SignupControllerTests
{
    [Fact]
    public async Task Signup_ValidViewModel_Returns201WithResultAndSignsInTheOwner()
    {
        var expected = new TenantProvisionedServiceModel(Guid.NewGuid(), "acme", Guid.NewGuid(), "owner@acme.test");
        var facade = new RecordingTenantFacade(expected);
        var controller = BuildController(facade, correlationHeader: Guid.NewGuid().ToString());

        var result = await controller.Signup(new SignupViewModel("acme", "Acme Inc", "owner@acme.test", "Correct-Horse-1"), CancellationToken.None);

        var created = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status201Created, created.StatusCode);
        Assert.Equal(expected, created.Value);
        Assert.True(facade.WasCalled);
    }

    [Fact]
    public async Task Signup_PropagatesTheGatewayMintedCorrelationHeaderToTheFacade()
    {
        var expected = new TenantProvisionedServiceModel(Guid.NewGuid(), "acme", Guid.NewGuid(), "owner@acme.test");
        var facade = new RecordingTenantFacade(expected);
        var correlationId = Guid.NewGuid();
        var controller = BuildController(facade, correlationHeader: correlationId.ToString());

        await controller.Signup(new SignupViewModel("acme", "Acme Inc", "owner@acme.test", "Correct-Horse-1"), CancellationToken.None);

        Assert.Equal(correlationId, facade.CapturedCorrelationId);
    }

    [Fact]
    public async Task Signup_MissingCorrelationHeader_MintsItsOwnRatherThanFailing()
    {
        var expected = new TenantProvisionedServiceModel(Guid.NewGuid(), "acme", Guid.NewGuid(), "owner@acme.test");
        var facade = new RecordingTenantFacade(expected);
        var controller = BuildController(facade, correlationHeader: null);

        await controller.Signup(new SignupViewModel("acme", "Acme Inc", "owner@acme.test", "Correct-Horse-1"), CancellationToken.None);

        Assert.NotEqual(Guid.Empty, facade.CapturedCorrelationId);
    }

    [Fact]
    public async Task Signup_UnparseableCorrelationHeader_MintsItsOwnRatherThanFailing()
    {
        var expected = new TenantProvisionedServiceModel(Guid.NewGuid(), "acme", Guid.NewGuid(), "owner@acme.test");
        var facade = new RecordingTenantFacade(expected);
        var controller = BuildController(facade, correlationHeader: "not-a-guid");

        await controller.Signup(new SignupViewModel("acme", "Acme Inc", "owner@acme.test", "Correct-Horse-1"), CancellationToken.None);

        Assert.NotEqual(Guid.Empty, facade.CapturedCorrelationId);
    }

    [Fact]
    public async Task Signup_FacadeThrowsValidation_PropagatesRatherThanSigningAnyoneIn()
    {
        var facade = new ThrowingTenantFacade();
        var controller = BuildController(facade, correlationHeader: null);

        await Assert.ThrowsAsync<ValidationException>(() =>
            controller.Signup(new SignupViewModel("acme", "Acme Inc", "owner@acme.test", "Correct-Horse-1"), CancellationToken.None));
    }

    private static SignupController BuildController(ITenantFacade facade, string? correlationHeader)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDataProtection();
        services.AddAuthentication(AuthCookieScheme.SchemeName)
            .AddCookie(AuthCookieScheme.SchemeName);
        var provider = services.BuildServiceProvider();

        var httpContext = new DefaultHttpContext { RequestServices = provider };
        if (correlationHeader is not null)
        {
            httpContext.Request.Headers[EdgeHeaderNames.CorrelationId] = correlationHeader;
        }

        return new SignupController(facade)
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext },
        };
    }

    private sealed class RecordingTenantFacade(TenantProvisionedServiceModel result) : ITenantFacade
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

    private sealed class ThrowingTenantFacade : ITenantFacade
    {
        public Task<TenantProvisionedServiceModel> ProvisionAsync(SignupViewModel viewModel, Guid correlationId, CancellationToken ct = default) =>
            throw new ValidationException([new FluentValidation.Results.ValidationFailure(nameof(viewModel.Slug), "This slug is already in use.")]);
    }
}
