using Microsoft.EntityFrameworkCore;
using PlannerPro.Shared.Tests.TestSupport;

namespace PlannerPro.Shared.Tests.Tenancy;

public sealed class SharedDbContextModelValidationTests
{
    [Fact]
    public void OnModelCreating_ThrowsWhenAnITenantScopedTypesInheritanceRootIsNotItselfITenantScoped()
    {
        var options = new DbContextOptionsBuilder<BrokenTenantHierarchyDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;

        using var context = new BrokenTenantHierarchyDbContext(options, StaticTenantContext.Bypass);

        // Model building is lazy — first access to .Model triggers OnModelCreating.
        Assert.Throws<InvalidOperationException>(() => context.Model);
    }
}
