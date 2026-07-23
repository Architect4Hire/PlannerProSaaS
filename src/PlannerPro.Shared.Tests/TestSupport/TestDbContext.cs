using Microsoft.EntityFrameworkCore;
using PlannerPro.Shared.Persistence;
using PlannerPro.Shared.Tenancy;

namespace PlannerPro.Shared.Tests.TestSupport;

internal sealed class TestDbContext(DbContextOptions<TestDbContext> options, ITenantContext tenant)
    : SharedDbContext(options, tenant)
{
    public DbSet<TenantScopedEntity> TenantScopedEntities => Set<TenantScopedEntity>();
}
