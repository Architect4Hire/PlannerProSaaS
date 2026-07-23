using Microsoft.EntityFrameworkCore;
using PlannerPro.Shared.Persistence;
using PlannerPro.Shared.Tenancy;

namespace PlannerPro.Shared.Tests.TestSupport;

/// <summary>
/// An inheritance root that does NOT implement <see cref="ITenantScoped"/>, with a derived type that
/// does — the exact shape <see cref="SharedDbContext"/>'s startup assertion exists to catch, since EF
/// Core can only apply <c>HasQueryFilter</c> to the root of a TPH hierarchy.
/// </summary>
internal abstract class UntenantedBase
{
    public Guid Id { get; set; }
}

internal sealed class WronglyScopedDerived : UntenantedBase, ITenantScoped
{
    public Guid TenantId { get; set; }
}

internal sealed class BrokenTenantHierarchyDbContext(
    DbContextOptions<BrokenTenantHierarchyDbContext> options, ITenantContext tenant)
    : SharedDbContext(options, tenant)
{
    public DbSet<WronglyScopedDerived> WronglyScopedDeriveds => Set<WronglyScopedDerived>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // EF Core only maps a base type into the hierarchy as its own entity type — i.e. only makes
        // WronglyScopedDerived.BaseType non-null — once it's explicitly known to the model, e.g. via
        // an explicit Entity<T>() call like this one (the ordinary trigger being a shared base across
        // multiple derived DbSets). This is also the exact "explicit Entity<T>() before base call"
        // shape SharedDbContext.OnModelCreating's XML doc warns about.
        modelBuilder.Entity<UntenantedBase>();
        base.OnModelCreating(modelBuilder);
    }
}
