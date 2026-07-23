namespace PlannerPro.Gateway.Tenancy;

/// <summary>
/// The gateway's one seam onto tenant + membership data, owned by <c>PlannerPro.Access</c> (ADR-0011:
/// "the gateway now needs read access to tenant and membership data — a dependency on Access on every
/// request path"). This is a sanctioned exception to "no synchronous service-to-service calls" — it's
/// the gateway resolving tenancy at the edge, not one bounded service calling another.
/// </summary>
public interface ITenantDirectory
{
    Task<TenantLookup?> ResolveTenantAsync(string slug, CancellationToken cancellationToken);

    Task<MembershipLookup?> ResolveMembershipAsync(Guid tenantId, Guid actorId, CancellationToken cancellationToken);
}
