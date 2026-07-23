using Microsoft.AspNetCore.Http;
using PlannerPro.Gateway.Middleware;
using PlannerPro.Shared.Http;
using PlannerPro.Shared.Tenancy;

namespace PlannerPro.Gateway.Tests.Middleware;

public sealed class TrustedHeaderStrippingMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_ClientSuppliedTrustedHeaders_AreAllRemoved()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers[TenantHeaderNames.TenantId] = Guid.NewGuid().ToString();
        context.Request.Headers[TenantHeaderNames.Slug] = "attacker-slug";
        context.Request.Headers[TenantHeaderNames.Role] = "Owner";
        context.Request.Headers[TenantHeaderNames.Plan] = "business";
        context.Request.Headers[TenantHeaderNames.Status] = "Active";
        context.Request.Headers[EdgeHeaderNames.ActorId] = Guid.NewGuid().ToString();

        var nextCalled = false;
        var middleware = new TrustedHeaderStrippingMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context);

        Assert.True(nextCalled);
        Assert.False(context.Request.Headers.ContainsKey(TenantHeaderNames.TenantId));
        Assert.False(context.Request.Headers.ContainsKey(TenantHeaderNames.Slug));
        Assert.False(context.Request.Headers.ContainsKey(TenantHeaderNames.Role));
        Assert.False(context.Request.Headers.ContainsKey(TenantHeaderNames.Plan));
        Assert.False(context.Request.Headers.ContainsKey(TenantHeaderNames.Status));
        Assert.False(context.Request.Headers.ContainsKey(EdgeHeaderNames.ActorId));
    }

    [Fact]
    public async Task InvokeAsync_NoTrustedHeadersPresent_CallsNextWithoutError()
    {
        var context = new DefaultHttpContext();
        var nextCalled = false;
        var middleware = new TrustedHeaderStrippingMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context);

        Assert.True(nextCalled);
    }
}
