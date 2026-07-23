using Microsoft.AspNetCore.Http;
using PlannerPro.Gateway.Middleware;
using PlannerPro.Shared.Http;

namespace PlannerPro.Gateway.Tests.Middleware;

public sealed class CorrelationIdMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_ClientSuppliedCorrelationId_IsDiscardedAndReplaced()
    {
        var spoofed = "attacker-supplied-value";
        var context = new DefaultHttpContext();
        context.Request.Headers[EdgeHeaderNames.CorrelationId] = spoofed;

        var middleware = new CorrelationIdMiddleware(_ => Task.CompletedTask);

        await middleware.InvokeAsync(context);

        var mintedForRequest = context.Request.Headers[EdgeHeaderNames.CorrelationId].ToString();
        Assert.NotEqual(spoofed, mintedForRequest);
        Assert.True(Guid.TryParse(mintedForRequest, out _));
    }

    [Fact]
    public async Task InvokeAsync_NoInboundCorrelationId_MintsOne()
    {
        var context = new DefaultHttpContext();
        var middleware = new CorrelationIdMiddleware(_ => Task.CompletedTask);

        await middleware.InvokeAsync(context);

        Assert.True(Guid.TryParse(context.Request.Headers[EdgeHeaderNames.CorrelationId].ToString(), out _));
    }

    // The response-echo behavior (via Response.OnStarting) needs a real request/response lifecycle to
    // fire correctly — a bare DefaultHttpContext doesn't reliably drive it. That's covered by
    // GatewayWebApplicationFactoryTests instead, against the real pipeline.
}
