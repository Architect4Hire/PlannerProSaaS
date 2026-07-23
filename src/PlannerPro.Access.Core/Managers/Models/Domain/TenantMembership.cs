using PlannerPro.Shared.Tenancy;

namespace PlannerPro.Access.Core.Managers.Models.Domain;

/// <summary>
/// Links a global <see cref="ApplicationUser"/> to one tenant, carrying everything about that
/// relationship that is inherently per-tenant (ADR-0010): <see cref="Role"/>,
/// <see cref="DefaultCapacityPoints"/>. This — joined to <c>Users</c> — is the only correct way to
/// enumerate a tenant's people; querying <c>ApplicationUser</c> directly would enumerate the whole
/// platform. Unique on <c>(TenantId, UserId)</c>.
/// </summary>
public sealed class TenantMembership : ITenantScoped
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    /// <summary>References the global <see cref="ApplicationUser.Id"/> — not itself tenant-scoped,
    /// since users aren't.</summary>
    public Guid UserId { get; set; }

    public TenantRole Role { get; set; } = TenantRole.Member;

    public MembershipStatus Status { get; set; } = MembershipStatus.Active;

    public int DefaultCapacityPoints { get; set; }
}
