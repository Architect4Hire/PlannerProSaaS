using Microsoft.AspNetCore.Mvc;
using PlannerPro.Access.Core.Facade;
using PlannerPro.Access.Core.Managers.Models.ServiceModels;

namespace PlannerPro.Access.Controllers;

/// <summary>
/// The contract the Gateway's <c>AccessTenantDirectory</c> is already written against
/// (<c>GET /internal/tenants/by-slug/{slug}</c>, <c>GET /internal/tenants/{tenantId}/memberships/{actorId}</c>).
/// Not proxied by any YARP route in <c>appsettings.json</c> — internal-only by convention, same as
/// Notifications having no public HTTP surface at all — and deliberately carries no
/// <c>[Authorize]</c>: the caller (the Gateway) isn't authenticated as a tenant member, it's the thing
/// establishing whether one exists. Trust here is network reachability, the same accepted,
/// not-yet-solved gap named in the HLD ("no network-level enforcement that services are gateway-only")
/// — not made worse by this type, not fixed by it either.
/// </summary>
[ApiController]
[Route("internal/tenants")]
public sealed class InternalTenantResolutionController(ITenantResolutionFacade facade) : ControllerBase
{
    [HttpGet("by-slug/{slug}")]
    public async Task<ActionResult<TenantLookupServiceModel>> GetBySlug(string slug, CancellationToken ct)
    {
        var tenant = await facade.ResolveTenantAsync(slug, ct);
        return tenant is null ? NotFound() : Ok(tenant);
    }

    [HttpGet("{tenantId:guid}/memberships/{actorId:guid}")]
    public async Task<ActionResult<MembershipLookupServiceModel>> GetMembership(Guid tenantId, Guid actorId, CancellationToken ct)
    {
        var membership = await facade.ResolveMembershipAsync(tenantId, actorId, ct);
        return membership is null ? NotFound() : Ok(membership);
    }
}
