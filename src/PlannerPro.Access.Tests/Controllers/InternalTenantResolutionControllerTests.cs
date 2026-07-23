using Microsoft.AspNetCore.Mvc;
using PlannerPro.Access.Controllers;
using PlannerPro.Access.Core.Business;
using PlannerPro.Access.Core.Facade;
using PlannerPro.Access.Core.Managers.Models.Domain;
using PlannerPro.Access.Tests.TestSupport;

namespace PlannerPro.Access.Tests.Controllers;

/// <summary>
/// Confirms the Gateway's exact 404-both-ways expectation (<c>MembershipLookup</c>'s doc comment: "a
/// null result from the directory (no membership found) and a null <c>TenantLookup</c> (unknown slug)
/// are handled identically") holds at this controller, not just conceptually.
/// </summary>
public sealed class InternalTenantResolutionControllerTests
{
    [Fact]
    public async Task GetBySlug_KnownSlug_Returns200WithTenantLookup()
    {
        var tenantId = Guid.NewGuid();
        var dataLayer = new StubTenantResolutionDataLayer
        {
            Tenant = new Tenant { Id = tenantId, TenantId = tenantId, Slug = "acme", Name = "Acme", Status = TenantStatus.Active },
        };
        var controller = BuildController(dataLayer);

        var result = await controller.GetBySlug("acme", CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var body = Assert.IsType<Core.Managers.Models.ServiceModels.TenantLookupServiceModel>(ok.Value);
        Assert.Equal(tenantId, body.TenantId);
    }

    [Fact]
    public async Task GetBySlug_UnknownSlug_Returns404()
    {
        var controller = BuildController(new StubTenantResolutionDataLayer());

        var result = await controller.GetBySlug("ghost", CancellationToken.None);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task GetMembership_ActiveMember_Returns200()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var dataLayer = new StubTenantResolutionDataLayer
        {
            Membership = new TenantMembership { Id = Guid.NewGuid(), TenantId = tenantId, UserId = userId, Role = TenantRole.Owner },
        };
        var controller = BuildController(dataLayer);

        var result = await controller.GetMembership(tenantId, userId, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result.Result);
    }

    [Fact]
    public async Task GetMembership_NoMembership_Returns404JustLikeAnUnknownTenant()
    {
        var controller = BuildController(new StubTenantResolutionDataLayer());

        var result = await controller.GetMembership(Guid.NewGuid(), Guid.NewGuid(), CancellationToken.None);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    private static InternalTenantResolutionController BuildController(StubTenantResolutionDataLayer dataLayer)
    {
        ITenantResolutionBusiness business = new TenantResolutionBusiness(dataLayer);
        ITenantResolutionFacade facade = new TenantResolutionFacade(business);
        return new InternalTenantResolutionController(facade);
    }
}
