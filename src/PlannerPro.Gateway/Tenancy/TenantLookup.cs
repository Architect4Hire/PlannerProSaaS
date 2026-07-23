namespace PlannerPro.Gateway.Tenancy;

/// <summary>Result of resolving a slug to a tenant, per ADR-0011. <c>Status</c> drives the read-only gate.</summary>
public sealed record TenantLookup(Guid TenantId, string Slug, string Status, string? Plan);
