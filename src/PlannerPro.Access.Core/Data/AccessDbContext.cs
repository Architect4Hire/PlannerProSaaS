using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PlannerPro.Access.Core.Managers.Models.Domain;
using PlannerPro.Shared.Persistence;
using PlannerPro.Shared.Tenancy;

namespace PlannerPro.Access.Core.Data;

/// <summary>
/// <c>accessdb</c> — the one database in the system with mixed scoping (ADR-0010). Identity's four
/// tables (<see cref="ApplicationUser"/> + <see cref="IdentityUserClaim{TKey}"/> +
/// <see cref="IdentityUserLogin{TKey}"/> + <see cref="IdentityUserToken{TKey}"/>) are GLOBAL and
/// carry no tenant filter — this is the deliberate exception per <c>.claude/rules/tenancy.md</c>, not
/// an oversight; do not "fix" it by adding one. <see cref="Tenant"/>, <see cref="TenantSettings"/>,
/// <see cref="TenantBranding"/>, <see cref="TenantMembership"/> and <see cref="Invitation"/> are all
/// <see cref="ITenantScoped"/> and get the automatic query filter <see cref="SharedDbContext"/>
/// applies by reflection — nothing below hand-writes a filter for them.
/// </summary>
public sealed class AccessDbContext(DbContextOptions<AccessDbContext> options, ITenantContext tenant)
    : SharedDbContext(options, tenant)
{
    public DbSet<ApplicationUser> Users => Set<ApplicationUser>();

    public DbSet<Tenant> Tenants => Set<Tenant>();

    public DbSet<TenantSettings> TenantSettings => Set<TenantSettings>();

    public DbSet<TenantBranding> TenantBrandings => Set<TenantBranding>();

    public DbSet<TenantMembership> TenantMemberships => Set<TenantMembership>();

    public DbSet<Invitation> Invitations => Set<Invitation>();

    /// <summary>
    /// Applies every <c>IEntityTypeConfiguration&lt;T&gt;</c> in this assembly (the four Identity
    /// configs + the five tenant-scoped domain configs — see <c>Data/Configurations/</c>), then calls
    /// <c>base.OnModelCreating</c> LAST so the reflection pass in <see cref="SharedDbContext"/> sees
    /// every entity already registered, per its own documented requirement.
    /// </summary>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AccessDbContext).Assembly);

        base.OnModelCreating(modelBuilder);
    }
}
