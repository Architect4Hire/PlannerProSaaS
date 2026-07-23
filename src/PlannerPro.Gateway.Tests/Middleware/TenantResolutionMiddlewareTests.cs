using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using PlannerPro.Gateway.Middleware;
using PlannerPro.Gateway.Tenancy;
using PlannerPro.Gateway.Tests.TestSupport;
using PlannerPro.Shared.Exceptions;
using PlannerPro.Shared.Tenancy;

namespace PlannerPro.Gateway.Tests.Middleware;

public sealed class TenantResolutionMiddlewareTests
{
    private static readonly JsonSerializerOptions DeserializeOptions = new() { PropertyNameCaseInsensitive = true };

    private static DefaultHttpContext CreateContext(string path, string method, Guid? actorId)
    {
        var context = new DefaultHttpContext
        {
            RequestServices = new ServiceCollection().BuildServiceProvider(),
        };
        context.Request.Path = path;
        context.Request.Method = method;
        context.Response.Body = new MemoryStream();

        if (actorId is not null)
        {
            var identity = new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, actorId.Value.ToString())], "Test");
            context.User = new ClaimsPrincipal(identity);
        }

        return context;
    }

    private static async Task<ErrorResponse> ReadErrorResponseAsync(DefaultHttpContext context)
    {
        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body, Encoding.UTF8);
        var json = await reader.ReadToEndAsync();
        return JsonSerializer.Deserialize<ErrorResponse>(json, DeserializeOptions)!;
    }

    [Fact]
    public async Task InvokeAsync_NonTenantScopedPath_PassesThroughWithoutTouchingDirectory()
    {
        var directory = new FakeTenantDirectory();
        var context = CreateContext("/api/me/tenants", "GET", Guid.NewGuid());
        var nextCalled = false;
        var middleware = new TenantResolutionMiddleware(_ => { nextCalled = true; return Task.CompletedTask; }, directory);

        await middleware.InvokeAsync(context);

        Assert.True(nextCalled);
        Assert.Equal(0, directory.ResolveTenantCallCount);
    }

    [Fact]
    public async Task InvokeAsync_UnknownSlug_Returns404WithGenericBody()
    {
        var directory = new FakeTenantDirectory();
        var context = CreateContext("/api/t/nosuchtenant/board", "GET", Guid.NewGuid());
        var nextCalled = false;
        var middleware = new TenantResolutionMiddleware(_ => { nextCalled = true; return Task.CompletedTask; }, directory);

        await middleware.InvokeAsync(context);

        Assert.False(nextCalled);
        Assert.Equal(StatusCodes.Status404NotFound, context.Response.StatusCode);
        var body = await ReadErrorResponseAsync(context);
        Assert.Equal("Not Found", body.Title);
        Assert.Null(body.Detail);
        Assert.Null(body.ErrorCode);
    }

    [Fact]
    public async Task InvokeAsync_MalformedSlug_Returns404WithoutCallingDirectory()
    {
        var directory = new FakeTenantDirectory();
        var context = CreateContext("/api/t/A/board", "GET", Guid.NewGuid());
        var middleware = new TenantResolutionMiddleware(_ => Task.CompletedTask, directory);

        await middleware.InvokeAsync(context);

        Assert.Equal(StatusCodes.Status404NotFound, context.Response.StatusCode);
        Assert.Equal(0, directory.ResolveTenantCallCount);
    }

    [Fact]
    public async Task InvokeAsync_ResolvedTenantButNoMembership_Returns404IdenticalToUnknownSlug()
    {
        var tenantId = Guid.NewGuid();
        var directory = new FakeTenantDirectory();
        directory.AddTenant(new TenantLookup(tenantId, "acme", "Active", "team"));

        var unknownSlugContext = CreateContext("/api/t/nosuchtenant/board", "GET", Guid.NewGuid());
        var nonMemberContext = CreateContext("/api/t/acme/board", "GET", Guid.NewGuid());

        await new TenantResolutionMiddleware(_ => Task.CompletedTask, directory).InvokeAsync(unknownSlugContext);
        await new TenantResolutionMiddleware(_ => Task.CompletedTask, directory).InvokeAsync(nonMemberContext);

        Assert.Equal(unknownSlugContext.Response.StatusCode, nonMemberContext.Response.StatusCode);
        var unknownSlugBody = await ReadErrorResponseAsync(unknownSlugContext);
        var nonMemberBody = await ReadErrorResponseAsync(nonMemberContext);
        Assert.Equal(unknownSlugBody, nonMemberBody);
    }

    [Fact]
    public async Task InvokeAsync_ActiveTenantWithMembership_ProjectsHeadersAndCallsNext()
    {
        var tenantId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var directory = new FakeTenantDirectory();
        directory.AddTenant(new TenantLookup(tenantId, "acme", "Active", "team"));
        directory.AddMembership(tenantId, actorId, new MembershipLookup("Admin"));

        var context = CreateContext("/api/t/acme/board", "GET", actorId);
        var nextCalled = false;
        var middleware = new TenantResolutionMiddleware(_ => { nextCalled = true; return Task.CompletedTask; }, directory);

        await middleware.InvokeAsync(context);

        Assert.True(nextCalled);
        Assert.Equal(tenantId.ToString(), context.Request.Headers[TenantHeaderNames.TenantId].ToString());
        Assert.Equal("acme", context.Request.Headers[TenantHeaderNames.Slug].ToString());
        Assert.Equal("Admin", context.Request.Headers[TenantHeaderNames.Role].ToString());
        Assert.Equal("Active", context.Request.Headers[TenantHeaderNames.Status].ToString());
        Assert.Equal("team", context.Request.Headers[TenantHeaderNames.Plan].ToString());
    }

    [Theory]
    [InlineData("Suspended")]
    [InlineData("PastDue")]
    [InlineData("Cancelled")]
    public async Task InvokeAsync_ReadOnlyTenantStatus_AllowsGet(string status)
    {
        var tenantId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var directory = new FakeTenantDirectory();
        directory.AddTenant(new TenantLookup(tenantId, "acme", status, "team"));
        directory.AddMembership(tenantId, actorId, new MembershipLookup("Admin"));

        var context = CreateContext("/api/t/acme/board", "GET", actorId);
        var nextCalled = false;
        var middleware = new TenantResolutionMiddleware(_ => { nextCalled = true; return Task.CompletedTask; }, directory);

        await middleware.InvokeAsync(context);

        Assert.True(nextCalled);
        Assert.NotEqual(StatusCodes.Status403Forbidden, context.Response.StatusCode);
    }

    [Theory]
    [InlineData("Suspended")]
    [InlineData("PastDue")]
    [InlineData("Cancelled")]
    public async Task InvokeAsync_ReadOnlyTenantStatus_RefusesMutatingVerbs(string status)
    {
        var tenantId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var directory = new FakeTenantDirectory();
        directory.AddTenant(new TenantLookup(tenantId, "acme", status, "team"));
        directory.AddMembership(tenantId, actorId, new MembershipLookup("Admin"));

        var context = CreateContext("/api/t/acme/board", "POST", actorId);
        var nextCalled = false;
        var middleware = new TenantResolutionMiddleware(_ => { nextCalled = true; return Task.CompletedTask; }, directory);

        await middleware.InvokeAsync(context);

        Assert.False(nextCalled);
        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
        var body = await ReadErrorResponseAsync(context);
        Assert.Equal("TENANT_READ_ONLY", body.ErrorCode);
    }

    [Fact]
    public async Task InvokeAsync_ActiveTenant_AllowsMutatingVerbs()
    {
        var tenantId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var directory = new FakeTenantDirectory();
        directory.AddTenant(new TenantLookup(tenantId, "acme", "Active", "team"));
        directory.AddMembership(tenantId, actorId, new MembershipLookup("Admin"));

        var context = CreateContext("/api/t/acme/board", "POST", actorId);
        var nextCalled = false;
        var middleware = new TenantResolutionMiddleware(_ => { nextCalled = true; return Task.CompletedTask; }, directory);

        await middleware.InvokeAsync(context);

        Assert.True(nextCalled);
    }

    [Fact]
    public async Task InvokeAsync_NoAuthenticatedActor_ThrowsRatherThanResolvingWithEmptyActor()
    {
        var directory = new FakeTenantDirectory();
        var context = CreateContext("/api/t/acme/board", "GET", actorId: null);
        var middleware = new TenantResolutionMiddleware(_ => Task.CompletedTask, directory);

        await Assert.ThrowsAsync<InvalidOperationException>(() => middleware.InvokeAsync(context));
    }
}
