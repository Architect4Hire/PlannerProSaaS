using Microsoft.AspNetCore.Http;

namespace PlannerPro.Shared.Tenancy;

/// <summary>
/// Populates <see cref="ITenantContext"/> from the gateway's trusted projected headers, on the HTTP
/// path only. Must run early in the ASP.NET Core pipeline, before any controller executes. This
/// middleware only populates context — it never rejects a request; requiring a resolved tenant before
/// a mutating action proceeds is a role-filter (Layer 5) concern, not this one.
/// </summary>
/// <remarks>
/// <para>
/// A bus consumer never passes through HTTP middleware at all — its tenant scope is established
/// entirely by the (separate, not-yet-built) Service Bus processor host from the event envelope, per
/// <c>.claude/rules/messaging.md</c>. This middleware has no role on that path.
/// </para>
/// <para>
/// Known gap: platform-admin routes (<c>/api/admin/*</c>) deliberately skip tenant-header projection at
/// the gateway, so on a normal host this leaves <see cref="ITenantContext.IsResolved"/> <c>false</c> —
/// tenant-scoped queries then see zero rows, not all rows. Supplying a bypass context for that request
/// scope is not solved here.
/// </para>
/// </remarks>
public sealed class TenantContextMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext httpContext, TenantContext tenantContext)
    {
        var tenantIdHeader = httpContext.Request.Headers[TenantHeaderNames.TenantId].FirstOrDefault();
        if (Guid.TryParse(tenantIdHeader, out var tenantId))
        {
            var slug = httpContext.Request.Headers[TenantHeaderNames.Slug].FirstOrDefault();
            var role = httpContext.Request.Headers[TenantHeaderNames.Role].FirstOrDefault();
            var plan = httpContext.Request.Headers[TenantHeaderNames.Plan].FirstOrDefault();
            var status = httpContext.Request.Headers[TenantHeaderNames.Status].FirstOrDefault();
            tenantContext.Resolve(tenantId, slug, role, plan, status);
        }

        await next(httpContext);
    }
}
