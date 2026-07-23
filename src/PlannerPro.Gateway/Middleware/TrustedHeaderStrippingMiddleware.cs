using Microsoft.AspNetCore.Http;
using PlannerPro.Shared.Http;
using PlannerPro.Shared.Tenancy;

namespace PlannerPro.Gateway.Middleware;

/// <summary>
/// Unconditionally removes any client-supplied tenant/actor headers, on every route tier, before
/// anything else in the pipeline could read a spoofed copy. This pairs with the projection each
/// downstream middleware performs (<see cref="ActorProjectionMiddleware"/>,
/// <c>TenantResolutionMiddleware</c>) — stripping without projecting breaks the app; projecting
/// without stripping lets a client assert its own tenancy. Never separate the two.
/// </summary>
public sealed class TrustedHeaderStrippingMiddleware(RequestDelegate next)
{
    private static readonly string[] TrustedHeaders =
    [
        TenantHeaderNames.TenantId,
        TenantHeaderNames.Slug,
        TenantHeaderNames.Role,
        TenantHeaderNames.Plan,
        TenantHeaderNames.Status,
        EdgeHeaderNames.ActorId,
    ];

    public async Task InvokeAsync(HttpContext httpContext)
    {
        foreach (var header in TrustedHeaders)
        {
            httpContext.Request.Headers.Remove(header);
        }

        await next(httpContext);
    }
}
