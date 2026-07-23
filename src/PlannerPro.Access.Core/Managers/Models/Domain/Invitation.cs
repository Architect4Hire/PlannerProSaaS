using PlannerPro.Shared.Tenancy;

namespace PlannerPro.Access.Core.Managers.Models.Domain;

/// <summary>
/// A pending (or resolved) invitation for someone to join a tenant at a given role. Accepted by
/// <see cref="Token"/> alone, before any tenant context exists — the accept-invitation flow itself is
/// Prompt 13's job; only the entity and migration ship now.
/// </summary>
/// <remarks>
/// <see cref="Token"/> is unique **globally**, not <c>(TenantId, Token)</c> — the same shape of
/// deliberate exception as <see cref="Tenant.Slug"/>, and for the identical reason: it is looked up
/// standalone, before any tenant is known.
/// </remarks>
public sealed class Invitation : ITenantScoped
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public required string Email { get; set; }

    public TenantRole Role { get; set; } = TenantRole.Member;

    public InvitationStatus Status { get; set; } = InvitationStatus.Pending;

    public required string Token { get; set; }

    public Guid InvitedByUserId { get; set; }

    public DateTimeOffset ExpiresAt { get; set; }

    public DateTimeOffset? AcceptedAtUtc { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }
}
