using PlannerPro.Shared.Authentication;

namespace PlannerPro.Gateway.Authentication;

/// <summary>
/// Wires cookie authentication at the edge, per ADR-0021: the browser authenticates with a cookie at
/// the gateway; services never see it, and never validate it themselves. The scheme name and cookie
/// options themselves live in <see cref="AuthCookieScheme"/> (in <c>PlannerPro.Shared</c>) because
/// <c>PlannerPro.Access</c> — the only service that ever issues this cookie, on login — must register
/// the byte-identical scheme for a cookie either of them touches to validate at the other.
/// </summary>
/// <remarks>
/// Data Protection itself (<c>AddDataProtection().SetApplicationName(...).PersistKeysToAzureBlobStorage(...)</c>)
/// is wired directly in this host's own <c>Program.cs</c>, not here — it needs the Aspire-registered
/// <c>BlobServiceClient</c> resolved from DI, which only exists once <c>Program.cs</c> has called
/// <c>builder.AddAzureBlobServiceClient(...)</c>. Calling <c>AddDataProtection()</c> a second time here
/// would double-register the builder against the same options instance; this type only adds the cookie
/// scheme itself.
/// </remarks>
public static class GatewayCookieAuthenticationSetup
{
    public static IServiceCollection AddGatewayCookieAuthentication(
        this IServiceCollection services, IHostEnvironment environment)
    {
        services.AddAuthCookieScheme(environment);

        return services;
    }
}
