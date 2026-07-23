using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PlannerPro.Contracts;
using PlannerPro.Portfolio.Core.Data;
using PlannerPro.Portfolio.Core.Facade;
using PlannerPro.Shared.Messaging;

namespace PlannerPro.Portfolio.Tests;

/// <summary>
/// Boots the REAL, unmodified composition root from Program.cs — same shape and reason as
/// <c>PlannerPro.Access.Tests.CompositionRootTests</c>: a DI-lifetime or wiring mistake should fail
/// loudly here rather than only surface once `aspire run` is used for real.
/// </summary>
/// <remarks>
/// <see cref="HostOptions.BackgroundServiceExceptionBehavior"/> is forced to
/// <see cref="BackgroundServiceExceptionBehavior.Ignore"/> here, test-only — same reasoning as
/// Access's composition-root test: <c>ServiceBusProcessorHost</c> tries to start receiving immediately
/// on host start, which fails against the placeholder, unreachable Service Bus namespace injected via
/// <see cref="WithPlaceholderServiceBusNamespace"/>, and ASP.NET Core's default is to stop (and
/// dispose) the whole host on any unhandled <c>BackgroundService</c> exception. Right default in
/// production; wrong for a test whose only job is checking DI wiring. See Access's composition-root
/// test for why that placeholder is a process environment variable rather than checked into
/// <c>appsettings.json</c>.
/// </remarks>
public sealed class CompositionRootTests
{
    [Fact]
    public async Task ServiceGraph_ResolvesWithoutError_FromTheRealCompositionRoot()
    {
        using var _ = WithPlaceholderServiceBusNamespace();

        await using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder => builder
                .UseEnvironment("Development")
                .ConfigureServices(services => services.Configure<HostOptions>(
                    options => options.BackgroundServiceExceptionBehavior = BackgroundServiceExceptionBehavior.Ignore)));

        using var scope = factory.Services.CreateScope();

        var exception = Record.Exception(() =>
        {
            scope.ServiceProvider.GetRequiredService<PortfolioDbContext>();
            scope.ServiceProvider.GetRequiredService<IClientFacade>();
        });

        Assert.Null(exception);
    }

    [Fact]
    public void ConsumerRegistry_HasAnEntryForTenantProvisioned()
    {
        // Catches the specific class of bug a copy-pasted registration invites: wiring
        // TenantProvisionedConsumer under the wrong event type, so ServiceBusProcessorHost would
        // silently dead-letter every real TenantProvisioned message with UnrecognizedEventType.
        using var placeholderNamespace = WithPlaceholderServiceBusNamespace();

        using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder => builder
                .UseEnvironment("Development")
                .ConfigureServices(services => services.Configure<HostOptions>(
                    options => options.BackgroundServiceExceptionBehavior = BackgroundServiceExceptionBehavior.Ignore)));

        var registry = factory.Services.GetRequiredService<IntegrationEventConsumerRegistry>();

        Assert.True(registry.TryGet(nameof(TenantProvisioned), out _));
    }

    private static IDisposable WithPlaceholderServiceBusNamespace() => new ServiceBusNamespaceEnvironmentVariable();

    private sealed class ServiceBusNamespaceEnvironmentVariable : IDisposable
    {
        private const string VariableName = "Aspire__Azure__Messaging__ServiceBus__FullyQualifiedNamespace";

        public ServiceBusNamespaceEnvironmentVariable() =>
            Environment.SetEnvironmentVariable(VariableName, "localhost.servicebus.windows.net");

        public void Dispose() => Environment.SetEnvironmentVariable(VariableName, null);
    }
}
