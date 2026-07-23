namespace PlannerPro.Access.Core.Managers.Models.Domain;

/// <summary>
/// The subscription lifecycle a <see cref="Tenant"/> moves through (ADR-0018, ADR-0020):
/// <c>Trialing</c> and <c>Active</c> permit writes; <c>PastDue</c>, <c>Suspended</c> and
/// <c>Cancelled</c> are read-only at the Gateway (see <c>TenantResolutionMiddleware</c>'s
/// <c>WritableStatuses</c> set, which must keep recognizing every name below).
/// </summary>
public enum TenantStatus
{
    Trialing,
    Active,
    PastDue,
    Suspended,
    Cancelled,
}
