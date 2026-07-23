namespace PlannerPro.Shared.Tenancy;

/// <summary>
/// Bypass context for migration, seeding, and the platform-admin surface — the only places
/// <c>IgnoreQueryFilters()</c>-equivalent behavior is allowed. Instantiate directly (<c>new
/// SystemTenantContext()</c>); it is never resolved through DI request scope.
/// </summary>
public sealed class SystemTenantContext : ITenantContext
{
    public Guid TenantId => Guid.Empty;

    public string? Slug => null;

    public string? Role => null;

    public string? Plan => null;

    public string? Status => null;

    public bool IsResolved => true;

    public bool BypassFilters => true;
}
