namespace PlannerPro.Gateway.Tenancy;

/// <summary>Result of resolving a caller's active membership in a tenant. A null result from the
/// directory (no membership found) and a null <see cref="TenantLookup"/> (unknown slug) are handled
/// identically by <see cref="TenantResolutionMiddleware"/> — both are 404, never distinguishable.</summary>
public sealed record MembershipLookup(string Role);
