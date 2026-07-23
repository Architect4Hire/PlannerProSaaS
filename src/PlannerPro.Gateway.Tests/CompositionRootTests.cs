using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using PlannerPro.Gateway.Middleware;

namespace PlannerPro.Gateway.Tests;

/// <summary>
/// Boots the REAL, unmodified composition root from Program.cs (unlike every other test here, which
/// goes through <c>PlannerPro.Gateway.Tests.TestSupport.GatewayTestFactory</c> and substitutes a fake
/// <c>ITenantDirectory</c>). That substitution is exactly why a DI-lifetime bug — <c>ITenantDirectory</c>
/// registered <c>AddScoped</c> while <see cref="TenantResolutionMiddleware"/> is a conventional
/// middleware constructed once against the pipeline's ROOT service provider — passed 61/61 tests while
/// the app was actually unable to serve a single request. This test targets that composition root
/// directly so a regression of the same shape can't hide behind the fake substitution again.
/// </summary>
public sealed class CompositionRootTests
{
    [Fact]
    public async Task TenantResolutionMiddleware_ConstructsFromRealCompositionRoot_WithoutScopeValidationFailure()
    {
        await using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder => builder.UseEnvironment("Development"));

        // Mirrors exactly what ASP.NET Core's UseMiddleware<T>() does the first time a conventional
        // middleware is invoked: ActivatorUtilities.CreateInstance against the app's root provider,
        // not a per-request scope.
        var exception = Record.Exception(() =>
            ActivatorUtilities.CreateInstance<TenantResolutionMiddleware>(
                factory.Services, (RequestDelegate)(_ => Task.CompletedTask)));

        Assert.Null(exception);
    }
}
