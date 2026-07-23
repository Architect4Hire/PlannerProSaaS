using Microsoft.AspNetCore.Http;

namespace PlannerPro.Gateway.Routing;

/// <summary>
/// Classifies a request path into a <see cref="RouteTier"/> by prefix, per ADR-0007's fixed route
/// list. Deliberately not driven by YARP route metadata — whether that's visible to ordinary
/// middleware before <c>MapReverseProxy()</c>'s own pipeline runs is a version-sensitive fact this
/// codebase hasn't verified, so classification uses a plain, unit-testable prefix match instead.
/// <c>RouteTableConsistencyTests</c> (Gateway tests) guards against this list drifting from
/// <c>appsettings.json</c>'s configured routes.
/// </summary>
public static class RouteTierClassifier
{
    private static readonly PathString[] AnonymousPrefixes =
    [
        "/api/ping",
        "/api/public",
        "/api/auth",
        "/api/signup",
        "/api/invitations",
    ];

    private static readonly PathString[] AuthenticatedNonTenantPrefixes =
    [
        "/api/me/tenants",
        "/api/admin",
    ];

    private static readonly PathString TenantScopedPrefix = "/api/t";

    public static RouteTier Classify(PathString path)
    {
        foreach (var prefix in AnonymousPrefixes)
        {
            if (path.StartsWithSegments(prefix))
            {
                return RouteTier.Anonymous;
            }
        }

        foreach (var prefix in AuthenticatedNonTenantPrefixes)
        {
            if (path.StartsWithSegments(prefix))
            {
                return RouteTier.AuthenticatedNonTenant;
            }
        }

        if (path.StartsWithSegments(TenantScopedPrefix))
        {
            return RouteTier.TenantScoped;
        }

        // Fail closed: an unlisted path requires authentication but skips tenant resolution, rather
        // than defaulting to anonymous access for a route nobody has explicitly classified yet.
        return RouteTier.AuthenticatedNonTenant;
    }
}
