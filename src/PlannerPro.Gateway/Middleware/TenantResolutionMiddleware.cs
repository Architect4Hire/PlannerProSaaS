using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using PlannerPro.Gateway.Routing;
using PlannerPro.Gateway.Tenancy;
using PlannerPro.Shared.Exceptions;
using PlannerPro.Shared.Tenancy;

namespace PlannerPro.Gateway.Middleware;

/// <summary>
/// The single place tenancy is resolved (ADR-0011). Acts only on <see cref="RouteTier.TenantScoped"/>
/// requests; every other tier is untouched. Assumes authentication has already been enforced upstream
/// by <c>UseAuthorization()</c> against each tenant-scoped route's <c>AuthorizationPolicy</c> — this
/// middleware does not re-check it, per the pipeline design, but fails loudly rather than silently
/// resolving against an empty actor id if that assumption is ever violated.
/// </summary>
public sealed class TenantResolutionMiddleware(RequestDelegate next, ITenantDirectory tenantDirectory)
{
    private static readonly HashSet<string> ReadOnlyStatuses =
        new(StringComparer.OrdinalIgnoreCase) { "Suspended", "PastDue", "Cancelled" };

    private static readonly HashSet<string> MutatingMethods =
        new(StringComparer.OrdinalIgnoreCase) { "POST", "PUT", "PATCH", "DELETE" };

    // Deliberately one shared, byte-identical response for both "unknown slug" and "no active
    // membership" — an attacker guessing at slugs must not be able to distinguish the two cases.
    private static readonly ErrorResponse NotFoundResponse = new("Not Found", StatusCodes.Status404NotFound, null, null, null);

    public async Task InvokeAsync(HttpContext httpContext)
    {
        if (RouteTierClassifier.Classify(httpContext.Request.Path) != RouteTier.TenantScoped)
        {
            await next(httpContext);
            return;
        }

        if (!TenantSlug.TryExtract(httpContext.Request.Path, out var slug, out _))
        {
            await WriteNotFoundAsync(httpContext);
            return;
        }

        var actorIdClaim = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(actorIdClaim, out var actorId))
        {
            throw new InvalidOperationException(
                "TenantResolutionMiddleware requires an authenticated caller with a valid NameIdentifier claim; " +
                "UseAuthorization() should have rejected this request before it reached here.");
        }

        var tenant = await tenantDirectory.ResolveTenantAsync(slug, httpContext.RequestAborted);
        if (tenant is null)
        {
            await WriteNotFoundAsync(httpContext);
            return;
        }

        var membership = await tenantDirectory.ResolveMembershipAsync(tenant.TenantId, actorId, httpContext.RequestAborted);
        if (membership is null)
        {
            await WriteNotFoundAsync(httpContext);
            return;
        }

        if (ReadOnlyStatuses.Contains(tenant.Status) && MutatingMethods.Contains(httpContext.Request.Method))
        {
            await WriteReadOnlyAsync(httpContext);
            return;
        }

        httpContext.Request.Headers[TenantHeaderNames.TenantId] = tenant.TenantId.ToString();
        httpContext.Request.Headers[TenantHeaderNames.Slug] = tenant.Slug;
        httpContext.Request.Headers[TenantHeaderNames.Role] = membership.Role;
        httpContext.Request.Headers[TenantHeaderNames.Status] = tenant.Status;
        if (tenant.Plan is not null)
        {
            httpContext.Request.Headers[TenantHeaderNames.Plan] = tenant.Plan;
        }

        await next(httpContext);
    }

    private static Task WriteNotFoundAsync(HttpContext httpContext)
    {
        httpContext.Response.StatusCode = NotFoundResponse.Status;
        return httpContext.Response.WriteAsJsonAsync(NotFoundResponse, httpContext.RequestAborted);
    }

    private static Task WriteReadOnlyAsync(HttpContext httpContext)
    {
        var response = new ErrorResponse(
            "Tenant is read-only",
            StatusCodes.Status403Forbidden,
            "This tenant's subscription status only allows reads and exports right now.",
            "TENANT_READ_ONLY",
            null);
        httpContext.Response.StatusCode = response.Status;
        return httpContext.Response.WriteAsJsonAsync(response, httpContext.RequestAborted);
    }
}
