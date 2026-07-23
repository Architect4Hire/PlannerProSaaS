using PlannerPro.Shared.Tenancy;

namespace PlannerPro.Portfolio.Tests.TestSupport;

internal sealed class StaticTenantContext : ITenantContext
{
    public static readonly StaticTenantContext Bypass = new() { BypassFilters = true, IsResolved = true };

    public Guid TenantId { get; init; }
    public string? Slug { get; init; }
    public string? Role { get; init; }
    public string? Plan { get; init; }
    public string? Status { get; init; }
    public bool IsResolved { get; init; }
    public bool BypassFilters { get; init; }

    public static StaticTenantContext For(Guid tenantId) => new() { TenantId = tenantId, IsResolved = true };
}
