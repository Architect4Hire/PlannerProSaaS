using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PlannerPro.Access.Core.Data;
using PlannerPro.Access.Core.Facade;
using PlannerPro.Access.Core.Managers.Models.Domain;

namespace PlannerPro.Access.Tests;

/// <summary>
/// Boots the REAL, unmodified composition root from Program.cs — the same shape of test as
/// PlannerPro.Gateway.Tests.CompositionRootTests, added for the same reason: a DI-lifetime or wiring
/// mistake (e.g. AddAccessCore's DbContext registration moving to Program.cs via the Aspire SQL Server
/// integration, or a service needing something Program.cs never registers) should fail loudly here
/// rather than only surface once `aspire run` is used for real. Resolves services from a scope rather
/// than making an HTTP request that would touch the DB — the placeholder `accessdb` connection string
/// in appsettings.json parses fine but has nothing listening behind it outside `aspire run`.
/// </summary>
/// <remarks>
/// <see cref="HostOptions.BackgroundServiceExceptionBehavior"/> is forced to
/// <see cref="BackgroundServiceExceptionBehavior.Ignore"/> here, test-only: <c>OutboxDispatcher</c>'s
/// poll loop runs its first iteration immediately on host start (before its first delay), so against
/// this placeholder unreachable <c>accessdb</c> it always throws a <c>SqlException</c> on the very
/// first poll — and ASP.NET Core's DEFAULT behavior is to stop (and dispose) the whole host on any
/// unhandled <c>BackgroundService</c> exception. That default is exactly right in production (fail
/// loudly rather than run half-broken); it's wrong for THIS test, whose only job is checking that DI
/// wiring resolves, not that a live outbox poll succeeds against real infrastructure.
/// </remarks>
public sealed class CompositionRootTests
{
    [Fact]
    public async Task ServiceGraph_ResolvesWithoutError_FromTheRealCompositionRoot()
    {
        await using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder => builder
                .UseEnvironment("Development")
                .ConfigureServices(services => services.Configure<HostOptions>(
                    options => options.BackgroundServiceExceptionBehavior = BackgroundServiceExceptionBehavior.Ignore)));

        using var scope = factory.Services.CreateScope();

        var exception = Record.Exception(() =>
        {
            scope.ServiceProvider.GetRequiredService<AccessDbContext>();
            scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            scope.ServiceProvider.GetRequiredService<SignInManager<ApplicationUser>>();
            scope.ServiceProvider.GetRequiredService<IAuthFacade>();
            scope.ServiceProvider.GetRequiredService<ITenantResolutionFacade>();
        });

        Assert.Null(exception);
    }
}
