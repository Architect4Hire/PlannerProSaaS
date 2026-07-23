using Microsoft.AspNetCore.Http;
using PlannerPro.Shared.Http;

namespace PlannerPro.Gateway.Middleware;

/// <summary>
/// Mints a fresh <see cref="EdgeHeaderNames.CorrelationId"/> for every request, including anonymous
/// ones. Per the audit-trail pattern, an inbound copy is always discarded and never trusted — this
/// runs before <see cref="TrustedHeaderStrippingMiddleware"/> and duplicates the strip for this one
/// header on purpose, since minting unconditionally already achieves it; the explicit removal below
/// just keeps this middleware correct in isolation regardless of ordering.
/// </summary>
public sealed class CorrelationIdMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext httpContext)
    {
        httpContext.Request.Headers.Remove(EdgeHeaderNames.CorrelationId);

        var correlationId = Guid.NewGuid();
        httpContext.Request.Headers[EdgeHeaderNames.CorrelationId] = correlationId.ToString();

        httpContext.Response.OnStarting(() =>
        {
            httpContext.Response.Headers[EdgeHeaderNames.CorrelationId] = correlationId.ToString();
            return Task.CompletedTask;
        });

        await next(httpContext);
    }
}
