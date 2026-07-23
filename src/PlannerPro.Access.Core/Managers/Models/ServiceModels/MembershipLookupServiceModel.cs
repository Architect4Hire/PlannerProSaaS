namespace PlannerPro.Access.Core.Managers.Models.ServiceModels;

/// <summary>Field name mirrors the Gateway's own <c>MembershipLookup</c> record exactly — see the XML
/// doc on <see cref="TenantLookupServiceModel"/> for why.</summary>
public sealed record MembershipLookupServiceModel(string Role);
