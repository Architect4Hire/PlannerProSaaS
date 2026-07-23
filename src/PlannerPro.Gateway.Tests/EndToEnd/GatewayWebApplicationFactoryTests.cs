using System.Net;
using PlannerPro.Gateway.Tests.TestSupport;

namespace PlannerPro.Gateway.Tests.EndToEnd;

/// <summary>
/// Exercises the real gateway pipeline (Program.cs, all middleware, real route table) through
/// <see cref="GatewayTestFactory"/>. Each test creates its own factory instance so call counts on the
/// shared <see cref="FakeTenantDirectory"/> never leak between tests.
/// </summary>
public sealed class GatewayWebApplicationFactoryTests
{
    [Fact]
    public async Task Ping_NoAuthHeader_Returns200()
    {
        await using var factory = new GatewayTestFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/ping");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task MeTenants_NoAuthHeader_Returns401()
    {
        await using var factory = new GatewayTestFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/me/tenants");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Admin_NoAuthHeader_Returns401()
    {
        await using var factory = new GatewayTestFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/admin/tenants");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Admin_AuthenticatedNonAdmin_Returns403()
    {
        await using var factory = new GatewayTestFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.ActorIdHeader, Guid.NewGuid().ToString());

        var response = await client.GetAsync("/api/admin/tenants");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Admin_AuthenticatedPlatformAdmin_PassesTheAdminGate()
    {
        await using var factory = new GatewayTestFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.ActorIdHeader, Guid.NewGuid().ToString());
        client.DefaultRequestHeaders.Add(TestAuthHandler.PlatformAdminHeader, "true");

        var response = await client.GetAsync("/api/admin/tenants");

        // Access doesn't exist yet, so this can't reach a real destination — the point of this test is
        // only that the platform-admin gate itself doesn't reject a genuine platform admin.
        Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.NotEqual(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task TenantScopedRoute_NoAuthHeader_Returns401BeforeAnyTenantLookup()
    {
        await using var factory = new GatewayTestFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/t/acme/board");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(0, factory.TenantDirectory.ResolveTenantCallCount);
    }

    [Fact]
    public async Task TenantScopedRoute_AuthenticatedNonMember_Returns404()
    {
        await using var factory = new GatewayTestFactory();
        factory.TenantDirectory.AddTenant(new(Guid.NewGuid(), "acme", "Active", "team"));
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.ActorIdHeader, Guid.NewGuid().ToString());

        var response = await client.GetAsync("/api/t/acme/board");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
