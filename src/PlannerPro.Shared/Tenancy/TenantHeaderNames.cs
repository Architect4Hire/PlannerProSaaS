namespace PlannerPro.Shared.Tenancy;

/// <summary>
/// The trusted headers the gateway projects inward after resolving tenancy (see
/// <c>.claude/rules/gateway.md</c>). Never read a tenant identifier from a body, query string, or
/// route value — only these headers, or a bus event envelope on the consumer path.
/// </summary>
public static class TenantHeaderNames
{
    public const string TenantId = "X-Tenant-Id";
    public const string Slug = "X-Tenant-Slug";
    public const string Role = "X-Tenant-Role";
    public const string Plan = "X-Tenant-Plan";
    public const string Status = "X-Tenant-Status";
}
