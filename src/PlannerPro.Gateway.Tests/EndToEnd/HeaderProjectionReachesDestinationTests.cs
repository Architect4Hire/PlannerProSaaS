using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using PlannerPro.Gateway.Tenancy;
using PlannerPro.Gateway.Tests.TestSupport;
using PlannerPro.Shared.Http;
using PlannerPro.Shared.Tenancy;

namespace PlannerPro.Gateway.Tests.EndToEnd;

/// <summary>
/// The one test proving header stripping + projection survive actual YARP proxying, not just what the
/// gateway's own middleware sees before <c>MapReverseProxy()</c> runs. The "access" cluster's
/// destination is overridden at test time to point at <see cref="FakeDownstreamServer"/>, a real
/// second Kestrel host that echoes back every header it received.
/// </summary>
public sealed class HeaderProjectionReachesDestinationTests
{
    [Fact]
    public async Task ProxiedRequest_DownstreamReceivesProjectedHeaders_NotClientSpoofedOnes()
    {
        var tenantId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var spoofedTenantId = Guid.NewGuid();

        await using var factory = new GatewayTestFactory();
        factory.TenantDirectory.AddTenant(new TenantLookup(tenantId, "acme", "Active", "team"));
        factory.TenantDirectory.AddMembership(tenantId, actorId, new MembershipLookup("Admin"));

        await using var downstream = await FakeDownstreamServer.StartAsync();

        var configuredFactory = factory.WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["ReverseProxy:Clusters:access:Destinations:destination1:Address"] = downstream.Address,
                })));

        using var client = configuredFactory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.ActorIdHeader, actorId.ToString());
        client.DefaultRequestHeaders.Add(TenantHeaderNames.TenantId, spoofedTenantId.ToString());
        client.DefaultRequestHeaders.Add(TenantHeaderNames.Role, "Owner");
        client.DefaultRequestHeaders.Add(EdgeHeaderNames.ActorId, Guid.NewGuid().ToString());

        var response = await client.GetAsync("/api/t/acme/board");
        response.EnsureSuccessStatusCode();

        var receivedHeaders = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();
        Assert.NotNull(receivedHeaders);

        Assert.Equal(tenantId.ToString(), receivedHeaders[TenantHeaderNames.TenantId]);
        Assert.NotEqual(spoofedTenantId.ToString(), receivedHeaders[TenantHeaderNames.TenantId]);
        Assert.Equal("acme", receivedHeaders[TenantHeaderNames.Slug]);
        Assert.Equal("Admin", receivedHeaders[TenantHeaderNames.Role]);
        Assert.NotEqual("Owner", receivedHeaders[TenantHeaderNames.Role]);
        Assert.Equal(actorId.ToString(), receivedHeaders[EdgeHeaderNames.ActorId]);
        Assert.True(Guid.TryParse(receivedHeaders[EdgeHeaderNames.CorrelationId], out _));
    }
}
