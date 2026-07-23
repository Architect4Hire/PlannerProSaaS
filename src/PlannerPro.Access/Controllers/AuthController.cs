using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PlannerPro.Access.Core.Facade;
using PlannerPro.Access.Core.Managers.Models.ServiceModels;
using PlannerPro.Access.Core.Managers.Models.ViewModels;
using PlannerPro.Shared.Authentication;
using PlannerPro.Shared.Exceptions;

namespace PlannerPro.Access.Controllers;

/// <summary>
/// Credential/token issuance (ADR-0021): the ONLY service that ever calls
/// <see cref="AuthenticationHttpContextExtensions.SignInAsync(HttpContext, string, ClaimsPrincipal)"/>
/// for <see cref="AuthCookieScheme.SchemeName"/> — the Gateway validates the resulting cookie on every
/// later request but never issues one itself. No tenant role is issued here; that's resolved
/// per-request by the Gateway when a <c>/api/t/{slug}/…</c> call is made.
/// </summary>
[ApiController]
[Route("api/auth")]
public sealed class AuthController(IAuthFacade authFacade) : ControllerBase
{
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<SessionServiceModel>> Login(LoginViewModel viewModel, CancellationToken ct)
    {
        var session = await authFacade.LoginAsync(viewModel, ct);
        if (session is null)
        {
            // Deliberately generic — an unknown email and a wrong password produce the exact same
            // response, so login isn't a user-enumeration oracle.
            var response = new ErrorResponse(
                "Invalid credentials", StatusCodes.Status401Unauthorized, null, "INVALID_CREDENTIALS", null);
            return Unauthorized(response);
        }

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, session.UserId.ToString()),
            new Claim(ClaimTypes.Email, session.Email),
            new Claim("IsPlatformAdmin", session.IsPlatformAdmin ? "true" : "false"),
        };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, AuthCookieScheme.SchemeName));
        await HttpContext.SignInAsync(AuthCookieScheme.SchemeName, principal);

        return Ok(session);
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(AuthCookieScheme.SchemeName);
        return NoContent();
    }
}
