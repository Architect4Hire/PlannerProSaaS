namespace PlannerPro.Shared.Tenancy;

/// <summary>
/// Bypass context for <c>dotnet ef</c> design-time tooling, via a future <c>IDesignTimeDbContextFactory
/// &lt;TContext&gt;</c> per service. A distinct type from <see cref="SystemTenantContext"/> purely so
/// design-time and true system/admin bypasses are distinguishable if either is ever logged.
/// </summary>
public sealed class DesignTimeTenantContext : ITenantContext
{
    public Guid TenantId => Guid.Empty;

    public string? Slug => null;

    public string? Role => null;

    public string? Plan => null;

    public string? Status => null;

    public bool IsResolved => true;

    public bool BypassFilters => true;
}
