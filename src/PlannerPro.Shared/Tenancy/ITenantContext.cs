namespace PlannerPro.Shared.Tenancy;

/// <summary>
/// The current request's (or bus message's, or system operation's) tenant scope. Injected as a scoped
/// service. Get-only by design — nothing outside this assembly has a compiled way to mutate it
/// mid-request; see <see cref="TenantContext"/> and <see cref="TenantContextMiddleware"/>.
/// </summary>
public interface ITenantContext
{
    /// <summary>
    /// <see cref="Guid.Empty"/> when <see cref="IsResolved"/> is <c>false</c> — this makes the global
    /// query filter fail closed (matches nothing) rather than needing nullable-Guid handling.
    /// </summary>
    Guid TenantId { get; }

    string? Slug { get; }

    string? Role { get; }

    string? Plan { get; }

    string? Status { get; }

    /// <summary>
    /// <c>true</c> once the tenant headers (HTTP path) or event envelope (bus path) have been read.
    /// </summary>
    bool IsResolved { get; }

    /// <summary>
    /// <c>true</c> only for <see cref="SystemTenantContext"/> and <see cref="DesignTimeTenantContext"/>
    /// — migration, seeding, and the platform-admin surface. Short-circuits every tenant query filter.
    /// </summary>
    bool BypassFilters { get; }
}
