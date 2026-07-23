using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;

namespace PlannerPro.Gateway.Routing;

/// <summary>
/// Parses the tenant slug out of a /api/t/{slug}/... path. Per ADR-0007, the gateway is the only
/// place a slug is parsed — no downstream service does this. A malformed or reserved slug is treated
/// exactly like an unknown one: <see cref="TryExtract"/> returns <see langword="false"/> and the
/// caller resolves to 404, never a distinguishable 400.
/// </summary>
public static partial class TenantSlug
{
    private static readonly HashSet<string> ReservedSlugs = new(StringComparer.OrdinalIgnoreCase)
    {
        "api", "auth", "admin", "app", "www", "t", "signup", "login", "health", "public", "assets", "static",
    };

    [GeneratedRegex("^[a-z0-9][a-z0-9-]{1,30}[a-z0-9]$")]
    private static partial Regex SlugPattern();

    public static bool TryExtract(PathString path, out string slug, out PathString remainder)
    {
        slug = string.Empty;
        remainder = PathString.Empty;

        if (!path.StartsWithSegments("/api/t", out var afterPrefix) || afterPrefix.Value is null)
        {
            return false;
        }

        var segments = afterPrefix.Value.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0)
        {
            return false;
        }

        var candidate = segments[0];
        if (!IsValid(candidate))
        {
            return false;
        }

        slug = candidate;
        remainder = "/" + string.Join('/', segments.Skip(1));
        return true;
    }

    public static bool IsValid(string candidate) =>
        !ReservedSlugs.Contains(candidate) && SlugPattern().IsMatch(candidate);
}
