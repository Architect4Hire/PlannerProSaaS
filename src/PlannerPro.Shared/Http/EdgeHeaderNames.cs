namespace PlannerPro.Shared.Http;

/// <summary>
/// The trusted actor/correlation headers the gateway projects inward after resolving identity (see
/// <c>.claude/rules/gateway.md</c>, <c>.claude/rules/audit.md</c>). Sibling to
/// <see cref="PlannerPro.Shared.Tenancy.TenantHeaderNames"/> — same contract, different concern: these two carry actor
/// identity and request correlation rather than tenant scope, and are projected on every route tier,
/// not just tenant-scoped ones. Never read a value from a client-supplied copy of either header; only
/// the gateway mints them.
/// </summary>
public static class EdgeHeaderNames
{
    public const string ActorId = "X-Actor-Id";
    public const string CorrelationId = "X-Correlation-Id";
}
