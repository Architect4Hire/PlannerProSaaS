namespace PlannerPro.Contracts;

/// <summary>
/// Raised once, in the same transaction as the tenant's Tenant/TenantSettings/TenantBranding/owner
/// TenantMembership rows, when a new tenant finishes signup. <see cref="Slug"/> and
/// <see cref="TenantName"/> are the minimal denormalized fields a consumer needs (e.g. Portfolio's
/// default "Internal" client) without calling back into Access. Root event — <c>CausationId</c> is
/// <c>null</c>.
/// </summary>
public sealed record TenantProvisioned(
    Guid Id,
    Guid TenantId,
    Guid CorrelationId,
    Guid? CausationId,
    Guid ActorId,
    string Slug,
    string TenantName) : IIntegrationEvent;
