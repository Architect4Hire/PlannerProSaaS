using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using PlannerPro.Gateway.Tenancy;

namespace PlannerPro.Gateway.Tests.TestSupport;

/// <summary>
/// Boots the real gateway pipeline (<c>Program.cs</c>, all middleware, real YARP config) with two
/// substitutions: <see cref="TestAuthHandler"/> in place of the real cookie scheme, and
/// <see cref="TenantDirectory"/> (a <see cref="FakeTenantDirectory"/>) in place of
/// <c>AccessTenantDirectory</c>/<c>CachedTenantDirectory</c> — so tests can prove the gateway's own
/// logic without a real cookie or a running Access instance.
/// </summary>
public sealed class GatewayTestFactory : WebApplicationFactory<Program>
{
    public FakeTenantDirectory TenantDirectory { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            services.AddAuthentication()
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });

            services.PostConfigure<AuthenticationOptions>(options =>
            {
                options.DefaultScheme = TestAuthHandler.SchemeName;
                options.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
                options.DefaultChallengeScheme = TestAuthHandler.SchemeName;
            });

            services.RemoveAll<ITenantDirectory>();
            services.AddSingleton<ITenantDirectory>(TenantDirectory);
        });
    }
}
