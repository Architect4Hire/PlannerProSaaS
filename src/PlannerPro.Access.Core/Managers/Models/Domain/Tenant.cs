using PlannerPro.Shared.Tenancy;

namespace PlannerPro.Access.Core.Managers.Models.Domain;

/// <summary>
/// A customer organization — the root of the <c>Tenant → Client → Project</c> hierarchy. The one
/// entity in the system where <see cref="TenantId"/> is not a reference to something else: it is set
/// equal to <see cref="Id"/>, so the automatic tenant query filter (<c>SharedDbContext</c>) means a
/// resolved caller can only ever see their own <see cref="Tenant"/> row. There is no enumeration
/// surface — the only ways in are by a caller's own resolved tenant, or by <see cref="Slug"/> under
/// the explicit <c>SystemTenantContext</c> bypass the internal Gateway-resolution endpoints use (see
/// <c>Controllers/InternalTenantResolutionController</c>), the same bypass category as migration and
/// seeding.
/// </summary>
/// <remarks>
/// <see cref="Slug"/> is unique **globally**, not <c>(TenantId, Slug)</c> — a deliberate, named
/// exception to "every uniqueness constraint is tenant-scoped" (`.claude/rules/tenancy.md`), because a
/// tenant's slug is what makes it discoverable before any tenant context exists at all; a tenant *is*
/// the tenant.
/// </remarks>
public sealed class Tenant : ITenantScoped
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public required string Slug { get; set; }

    public required string Name { get; set; }

    public TenantStatus Status { get; set; } = TenantStatus.Trialing;

    /// <summary>References Billing's Plan catalog by id. No foreign key — Billing owns that table in
    /// its own database, and a cross-database FK is impossible (and would violate "no shared
    /// database" even if it weren't).</summary>
    public Guid? PlanId { get; set; }

    public DateTimeOffset? TrialEndsAt { get; set; }

    /// <summary>Reserved for a future Stripe integration (ADR-0018). Stays NULL and unused today —
    /// do not populate or read this outside a Billing/Stripe feature.</summary>
    public string? StripeCustomerId { get; set; }

    /// <summary>Reserved for a future Stripe integration (ADR-0018). Stays NULL and unused today.</summary>
    public string? StripeSubscriptionId { get; set; }

    /// <summary>Reserved for a future Stripe integration (ADR-0018). Stays NULL and unused today.</summary>
    public DateTimeOffset? CurrentPeriodEndsAt { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }
}
