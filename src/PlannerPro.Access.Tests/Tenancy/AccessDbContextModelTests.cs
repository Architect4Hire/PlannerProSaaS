using PlannerPro.Access.Core.Managers.Models.Domain;
using PlannerPro.Access.Tests.TestSupport;
using PlannerPro.Shared.Tenancy;

namespace PlannerPro.Access.Tests.Tenancy;

/// <summary>
/// Mirrors <c>PlannerPro.Shared.Tests.Tenancy.TenantScopedModelReflectionTests</c> for
/// <c>accessdb</c>'s specific mixed-scoping shape: every tenant-scoped domain type gets the automatic
/// filter, and <see cref="ApplicationUser"/> — the deliberate global exception — does not.
/// </summary>
public sealed class AccessDbContextModelTests : IDisposable
{
    private readonly SqliteAccessDbContextFactory _factory = new();

    [Theory]
    [InlineData(typeof(Tenant))]
    [InlineData(typeof(TenantSettings))]
    [InlineData(typeof(TenantBranding))]
    [InlineData(typeof(TenantMembership))]
    [InlineData(typeof(Invitation))]
    public void TenantScopedDomainType_HasTheTenantQueryFilter(Type entityType)
    {
        using var context = _factory.CreateContext();
        var entity = context.Model.FindEntityType(entityType)!;

        Assert.True(typeof(ITenantScoped).IsAssignableFrom(entity.ClrType));
        Assert.Contains(entity.GetDeclaredQueryFilters(), f => f.Key as string == "Tenant");
    }

    [Fact]
    public void ApplicationUser_DeliberatelyHasNoTenantFilter()
    {
        using var context = _factory.CreateContext();
        var entity = context.Model.FindEntityType(typeof(ApplicationUser))!;

        Assert.False(typeof(ITenantScoped).IsAssignableFrom(entity.ClrType));
        Assert.Empty(entity.GetDeclaredQueryFilters());
    }

    public void Dispose() => _factory.Dispose();
}
