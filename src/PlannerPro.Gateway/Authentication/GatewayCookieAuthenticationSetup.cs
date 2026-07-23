using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;

namespace PlannerPro.Gateway.Authentication;

/// <summary>
/// Wires cookie authentication at the edge, per ADR-0021: the browser authenticates with a cookie at
/// the gateway; services never see it, and never validate it themselves.
/// </summary>
/// <remarks>
/// <para>
/// <c>OnRedirectToLogin</c>/<c>OnRedirectToAccessDenied</c> are overridden to return a bare 401/403 —
/// the cookie handler's default behavior (HTML redirect to a login page) is wrong for an API gateway
/// with no page of its own to redirect to.
/// </para>
/// <para>
/// <b>Not solved here:</b> Data Protection key-ring sharing between this gateway and the future
/// <c>PlannerPro.Access</c> instance that will issue this cookie. <c>SetApplicationName</c> is the
/// minimal forward-compatible step; without shared key persistence (a file share, blob container, or
/// similar), a cookie won't even survive this process restarting, let alone validate against a
/// separately-running Access instance. That's a Prompt-5-or-later decision, not resolved by this type.
/// </para>
/// </remarks>
public static class GatewayCookieAuthenticationSetup
{
    public const string SchemeName = ".PlannerPro.Auth";

    public static IServiceCollection AddGatewayCookieAuthentication(
        this IServiceCollection services, IHostEnvironment environment)
    {
        services.AddDataProtection().SetApplicationName("PlannerPro");

        services.AddAuthentication(SchemeName)
            .AddCookie(SchemeName, options =>
            {
                options.Cookie.Name = SchemeName;
                options.Cookie.HttpOnly = true;
                options.Cookie.SameSite = SameSiteMode.Strict;
                options.Cookie.SecurePolicy = environment.IsDevelopment()
                    ? CookieSecurePolicy.SameAsRequest
                    : CookieSecurePolicy.Always;

                options.Events.OnRedirectToLogin = context =>
                {
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    return Task.CompletedTask;
                };
                options.Events.OnRedirectToAccessDenied = context =>
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    return Task.CompletedTask;
                };
            });

        return services;
    }
}
