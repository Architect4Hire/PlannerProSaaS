using Microsoft.EntityFrameworkCore;
using PlannerPro.Portfolio.Core.Managers.Models.Domain;
using PlannerPro.Shared.Persistence;
using PlannerPro.Shared.Tenancy;

namespace PlannerPro.Portfolio.Core.Data;

/// <summary><c>portfoliodb</c> — entirely tenant-scoped, unlike <c>accessdb</c>'s mixed identity
/// slice. <see cref="Client"/> is <see cref="ITenantScoped"/> and gets the automatic query filter
/// <see cref="SharedDbContext"/> applies by reflection.</summary>
public sealed class PortfolioDbContext(DbContextOptions<PortfolioDbContext> options, ITenantContext tenant)
    : SharedDbContext(options, tenant)
{
    public DbSet<Client> Clients => Set<Client>();

    /// <summary>Applies every <c>IEntityTypeConfiguration&lt;T&gt;</c> in this assembly, then calls
    /// <c>base.OnModelCreating</c> LAST so the reflection pass in <see cref="SharedDbContext"/> sees
    /// every entity already registered, per its own documented requirement.</summary>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PortfolioDbContext).Assembly);

        base.OnModelCreating(modelBuilder);
    }
}
