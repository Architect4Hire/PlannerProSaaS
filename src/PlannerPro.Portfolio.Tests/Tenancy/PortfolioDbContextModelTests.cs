using PlannerPro.Portfolio.Core.Managers.Models.Domain;
using PlannerPro.Portfolio.Tests.TestSupport;
using PlannerPro.Shared.Tenancy;

namespace PlannerPro.Portfolio.Tests.Tenancy;

/// <summary>Mirrors <c>PlannerPro.Access.Tests.Tenancy.AccessDbContextModelTests</c> — portfoliodb has
/// no global/unfiltered exception (unlike accessdb's Identity slice), so every domain type here is
/// expected to carry the automatic tenant query filter.</summary>
public sealed class PortfolioDbContextModelTests : IDisposable
{
    private readonly SqlitePortfolioDbContextFactory _factory = new();

    [Theory]
    [InlineData(typeof(Client))]
    public void TenantScopedDomainType_HasTheTenantQueryFilter(Type entityType)
    {
        using var context = _factory.CreateContext();
        var entity = context.Model.FindEntityType(entityType)!;

        Assert.True(typeof(ITenantScoped).IsAssignableFrom(entity.ClrType));
        Assert.Contains(entity.GetDeclaredQueryFilters(), f => f.Key as string == "Tenant");
    }

    public void Dispose() => _factory.Dispose();
}
