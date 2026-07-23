using System.Text.Json;
using PlannerPro.Gateway.Routing;

namespace PlannerPro.Gateway.Tests;

/// <summary>
/// Guards against the two-places-of-truth this design deliberately accepts: a route's
/// <c>AuthorizationPolicy</c> in <c>appsettings.json</c> must agree with what
/// <see cref="RouteTierClassifier"/> (a hand-written prefix list) says about that same path. If they
/// ever drift, a route could require auth in one place and not the other.
/// </summary>
public sealed class RouteTableConsistencyTests
{
    [Fact]
    public void EveryConfiguredRoute_AuthorizationPolicyAgreesWithRouteTierClassifier()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "GatewayAppSettings.json");
        using var document = JsonDocument.Parse(File.ReadAllText(path));

        var routes = document.RootElement.GetProperty("ReverseProxy").GetProperty("Routes");

        var checkedAny = false;
        foreach (var route in routes.EnumerateObject())
        {
            checkedAny = true;
            var matchPath = route.Value.GetProperty("Match").GetProperty("Path").GetString()!;
            var samplePath = matchPath.Replace("{**catch-all}", "sample-segment");
            var tier = RouteTierClassifier.Classify(samplePath);

            var hasAuthorizationPolicy = route.Value.TryGetProperty("AuthorizationPolicy", out _);

            if (hasAuthorizationPolicy)
            {
                Assert.True(
                    tier is RouteTier.AuthenticatedNonTenant or RouteTier.TenantScoped,
                    $"Route '{route.Name}' ({matchPath}) has an AuthorizationPolicy but " +
                    $"RouteTierClassifier says it's {tier}.");
            }
            else
            {
                Assert.True(
                    tier == RouteTier.Anonymous,
                    $"Route '{route.Name}' ({matchPath}) has no AuthorizationPolicy but " +
                    $"RouteTierClassifier says it's {tier}, not Anonymous.");
            }
        }

        Assert.True(checkedAny, "Expected at least one configured route to check.");
    }
}
