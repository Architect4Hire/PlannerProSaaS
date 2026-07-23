using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace PlannerPro.Gateway.Tests.TestSupport;

/// <summary>
/// Fake authentication handler standing in for the real cookie scheme in tests. Identity is carried
/// per-request via headers (rather than static/shared state) so tests can run in parallel without
/// interfering with each other. Absence of <see cref="ActorIdHeader"/> means "unauthenticated" — tests
/// exercising the 401 paths simply omit it.
/// </summary>
public sealed class TestAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "Test";
    public const string ActorIdHeader = "X-Test-Actor-Id";
    public const string PlatformAdminHeader = "X-Test-Is-Platform-Admin";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(ActorIdHeader, out var actorIdValues) ||
            !Guid.TryParse(actorIdValues.FirstOrDefault(), out var actorId))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, actorId.ToString()) };
        if (Request.Headers.TryGetValue(PlatformAdminHeader, out var adminValues) &&
            bool.TryParse(adminValues.FirstOrDefault(), out var isAdmin) && isAdmin)
        {
            claims.Add(new Claim("IsPlatformAdmin", "true"));
        }

        var identity = new ClaimsIdentity(claims, SchemeName);
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
