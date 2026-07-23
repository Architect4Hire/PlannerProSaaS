namespace PlannerPro.Access.Core.Managers.Models.Domain;

/// <summary>
/// A caller's role within one tenant (CLAUDE.md's role matrix). Per-tenant, not global — lives on
/// <see cref="TenantMembership"/>, never on <see cref="ApplicationUser"/> (ADR-0010).
/// </summary>
public enum TenantRole
{
    Viewer,
    Member,
    Admin,
    Owner,
}
