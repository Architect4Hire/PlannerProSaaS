using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PlannerPro.Access.Core.Facade;
using PlannerPro.Access.Core.Managers.Models.ServiceModels;
using PlannerPro.Access.Core.Managers.Models.ViewModels;
using PlannerPro.Shared.Authentication;
using PlannerPro.Shared.Http;

namespace PlannerPro.Access.Controllers;

/// <summary>
/// Provisions a brand-new tenant end to end (tenant + settings + branding + owner membership, one
/// transaction, one outbox row) and signs the new owner in — same
/// <see cref="AuthenticationHttpContextExtensions.SignInAsync(HttpContext, string, ClaimsPrincipal)"/>
/// call as <see cref="AuthController"/>'s login, since this IS the new owner's first session. An
/// anonymous route (<c>/api/signup</c>, ADR-0007) — the tenant doesn't exist yet, so there's nothing
/// for the Gateway to resolve.
/// </summary>
[ApiController]
[Route("api/signup")]
public sealed class SignupController(ITenantFacade tenantFacade) : ControllerBase
{
    [HttpPost]
    [AllowAnonymous]
    public async Task<ActionResult<TenantProvisionedServiceModel>> Signup(SignupViewModel viewModel, CancellationToken ct)
    {
        // The gateway always mints X-Correlation-Id, even on anonymous routes (CorrelationIdMiddleware
        // runs before route classification) — read it explicitly rather than trusting any other header
        // on this path, per .claude/rules/tenancy.md's "exactly two sources of TenantId" reasoning
        // extended to correlation: nothing here is ambient except what the gateway itself projected.
        var correlationId = Guid.TryParse(Request.Headers[EdgeHeaderNames.CorrelationId], out var parsed)
            ? parsed
            : Guid.NewGuid();

        var result = await tenantFacade.ProvisionAsync(viewModel, correlationId, ct);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, result.OwnerUserId.ToString()),
            new Claim(ClaimTypes.Email, result.OwnerEmail),
            new Claim("IsPlatformAdmin", "false"),
        };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, AuthCookieScheme.SchemeName));
        await HttpContext.SignInAsync(AuthCookieScheme.SchemeName, principal);

        return StatusCode(StatusCodes.Status201Created, result);
    }
}
