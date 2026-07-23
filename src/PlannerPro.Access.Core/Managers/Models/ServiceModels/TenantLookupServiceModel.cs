namespace PlannerPro.Access.Core.Managers.Models.ServiceModels;

/// <summary>
/// Field names deliberately mirror the Gateway's own <c>TenantLookup</c> record
/// (<c>PlannerPro.Gateway.Tenancy</c>) exactly — the two are loosely coupled via JSON shape, not a
/// shared assembly, matching how the Gateway already declared this contract independently of Access.
/// </summary>
public sealed record TenantLookupServiceModel(Guid TenantId, string Slug, string Status, string? Plan);
