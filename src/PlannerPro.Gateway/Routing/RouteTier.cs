namespace PlannerPro.Gateway.Routing;

/// <summary>
/// Which of the gateway's three resolution tiers a request falls into. See
/// <c>.claude/rules/gateway.md</c> and ADR-0007/ADR-0011 for the route list behind each tier.
/// </summary>
public enum RouteTier
{
    /// <summary>No authentication, no tenant resolution: /api/ping, /api/public/*, /api/auth/*, /api/signup, /api/invitations/*.</summary>
    Anonymous,

    /// <summary>Authenticated caller required, no tenant resolution: /api/me/tenants, /api/admin/*.</summary>
    AuthenticatedNonTenant,

    /// <summary>Authenticated caller required, full tenant resolution: /api/t/{slug}/**.</summary>
    TenantScoped,
}
