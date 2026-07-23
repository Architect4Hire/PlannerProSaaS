using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using PlannerPro.Shared.Http;

namespace PlannerPro.Gateway.Middleware;

/// <summary>
/// Projects <see cref="EdgeHeaderNames.ActorId"/> from the authenticated cookie principal, on every
/// route tier — not just tenant-scoped ones, since /api/me/tenants and /api/admin/* also need to know
/// who is calling. Runs after <see cref="TrustedHeaderStrippingMiddleware"/> and after
/// <c>UseAuthentication()</c> so <see cref="HttpContext.User"/> is populated. No-ops for an
/// unauthenticated caller on an anonymous route — there is no actor to project.
/// </summary>
public sealed class ActorProjectionMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext httpContext)
    {
        if (httpContext.User.Identity?.IsAuthenticated == true)
        {
            var actorId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (actorId is not null)
            {
                httpContext.Request.Headers[EdgeHeaderNames.ActorId] = actorId;
            }
        }

        await next(httpContext);
    }
}
