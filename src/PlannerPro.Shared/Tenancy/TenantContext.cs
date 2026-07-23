namespace PlannerPro.Shared.Tenancy;

/// <summary>
/// The real per-request <see cref="ITenantContext"/>. The type is public only because
/// <see cref="TenantContextMiddleware.InvokeAsync"/> must be a public method (ASP.NET Core's
/// convention-based middleware activation requires it) and so needs a public parameter type — but
/// <see cref="Resolve"/> is <c>internal</c>, so no other assembly has a compiled call site to mutate
/// it. Every consumer outside this assembly is registered against the get-only
/// <see cref="ITenantContext"/> interface via DI and never sees this type at all.
/// </summary>
public sealed class TenantContext : ITenantContext
{
    public Guid TenantId { get; private set; }

    public string? Slug { get; private set; }

    public string? Role { get; private set; }

    public string? Plan { get; private set; }

    public string? Status { get; private set; }

    public bool IsResolved { get; private set; }

    public bool BypassFilters => false;

    internal void Resolve(Guid tenantId, string? slug, string? role, string? plan, string? status)
    {
        TenantId = tenantId;
        Slug = slug;
        Role = role;
        Plan = plan;
        Status = status;
        IsResolved = true;
    }
}
