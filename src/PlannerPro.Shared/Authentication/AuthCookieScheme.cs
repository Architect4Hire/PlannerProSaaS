using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace PlannerPro.Shared.Authentication;

/// <summary>
/// The one cookie authentication scheme shared by exactly two processes, per ADR-0021: the Gateway,
/// which validates it on every subsequent request, and <c>PlannerPro.Access</c>, which is the only
/// service that ever issues it (on login). Both must register byte-identical scheme name and cookie
/// options — a name or option drifting between the two would silently break every session — so this
/// lives here once instead of being copied.
/// </summary>
/// <remarks>
/// Deliberately does NOT call <see cref="M:Microsoft.Extensions.DependencyInjection.DataProtectionServiceCollectionExtensions.AddDataProtection(IServiceCollection)"/>
/// — Data Protection key-ring persistence (<c>PersistKeysToAzureBlobStorage</c>, per ADR-0021's
/// key-ring-sharing gap) differs enough in its DI shape (needs the Aspire-registered blob client) that
/// each host's own <c>Program.cs</c> wires it explicitly. <see cref="DataProtectionApplicationName"/>
/// is exposed so both hosts pass the same value to <c>SetApplicationName</c> without duplicating the
/// literal.
/// </remarks>
public static class AuthCookieScheme
{
    public const string SchemeName = ".PlannerPro.Auth";

    public const string DataProtectionApplicationName = "PlannerPro";

    public static IServiceCollection AddAuthCookieScheme(this IServiceCollection services, IHostEnvironment environment)
    {
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
