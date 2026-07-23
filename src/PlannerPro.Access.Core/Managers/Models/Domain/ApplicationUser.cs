using Microsoft.AspNetCore.Identity;

namespace PlannerPro.Access.Core.Managers.Models.Domain;

/// <summary>
/// One account per email address, platform-wide (ADR-0010) — the deliberate, unfiltered exception in
/// an otherwise entirely tenant-scoped database. Deliberately does NOT implement
/// <see cref="PlannerPro.Shared.Tenancy.ITenantScoped"/>; the base <see cref="Data.AccessDbContext"/>
/// applies no query filter to it, and it must never be reached through a tenant-scoped query — any
/// "who is in this tenant" lookup goes through <see cref="TenantMembership"/> joined to this type,
/// never <c>DbSet&lt;ApplicationUser&gt;</c> directly, which would enumerate the entire platform.
/// </summary>
/// <remarks>
/// Backed by ASP.NET Core Identity's <c>UserOnlyStore&lt;ApplicationUser, AccessDbContext, Guid&gt;</c>
/// — deliberately no <see cref="IdentityRole"/> at all, because role is per-tenant
/// (<see cref="TenantMembership.Role"/>), never global. Per the RESTRICTION this type was built under:
/// no <c>IsAdmin</c> and no <c>DefaultCapacityPoints</c> here — both are per-tenant and live on
/// <see cref="TenantMembership"/>. <see cref="IsPlatformAdmin"/> is the one legitimately global flag,
/// for platform staff.
/// </remarks>
public sealed class ApplicationUser : IdentityUser<Guid>
{
    public bool IsPlatformAdmin { get; set; }
}
