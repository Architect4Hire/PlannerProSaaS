using PlannerPro.Access.Core.Business;
using PlannerPro.Access.Core.Managers.Models.Domain;
using PlannerPro.Access.Tests.TestSupport;

namespace PlannerPro.Access.Tests.Business;

public sealed class TenantResolutionBusinessTests
{
    [Fact]
    public async Task ResolveTenantAsync_KnownSlug_MapsToServiceModel()
    {
        var tenantId = Guid.NewGuid();
        var dataLayer = new StubTenantResolutionDataLayer
        {
            Tenant = new Tenant { Id = tenantId, TenantId = tenantId, Slug = "acme", Name = "Acme", Status = TenantStatus.Active },
        };
        var business = new TenantResolutionBusiness(dataLayer);

        var result = await business.ResolveTenantAsync("acme");

        Assert.NotNull(result);
        Assert.Equal(tenantId, result!.TenantId);
        Assert.Equal("acme", result.Slug);
        Assert.Equal("Active", result.Status);
    }

    [Fact]
    public async Task ResolveTenantAsync_UnknownSlug_ReturnsNull()
    {
        var business = new TenantResolutionBusiness(new StubTenantResolutionDataLayer());

        var result = await business.ResolveTenantAsync("no-such-tenant");

        Assert.Null(result);
    }

    [Fact]
    public async Task ResolveMembershipAsync_ActiveMember_MapsRole()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var dataLayer = new StubTenantResolutionDataLayer
        {
            Membership = new TenantMembership { Id = Guid.NewGuid(), TenantId = tenantId, UserId = userId, Role = TenantRole.Admin },
        };
        var business = new TenantResolutionBusiness(dataLayer);

        var result = await business.ResolveMembershipAsync(tenantId, userId);

        Assert.NotNull(result);
        Assert.Equal("Admin", result!.Role);
    }

    [Fact]
    public async Task ResolveMembershipAsync_NoMembership_ReturnsNull()
    {
        var business = new TenantResolutionBusiness(new StubTenantResolutionDataLayer());

        var result = await business.ResolveMembershipAsync(Guid.NewGuid(), Guid.NewGuid());

        Assert.Null(result);
    }
}
