using System.Net;

namespace PlannerPro.Gateway.Tenancy;

/// <summary>
/// The real <see cref="ITenantDirectory"/>, calling <c>PlannerPro.Access</c>'s internal resolution
/// endpoints over HTTP (service discovery target <c>http://access</c>, wired via
/// <c>AddServiceDefaults()</c>'s <c>ConfigureHttpClientDefaults</c> for discovery + resilience).
/// </summary>
/// <remarks>
/// The endpoints below (<c>GET /internal/tenants/by-slug/{slug}</c>,
/// <c>GET /internal/tenants/{tenantId}/memberships/{actorId}</c>) are backed by
/// <c>PlannerPro.Access</c>'s <c>InternalTenantResolutionController</c> — internal-only, not proxied by
/// any client-facing YARP route, trusted purely by network reachability (see that controller's own
/// remarks for the accepted risk this implies until network-level gateway-only enforcement exists).
/// </remarks>
public sealed class AccessTenantDirectory(HttpClient httpClient) : ITenantDirectory
{
    public async Task<TenantLookup?> ResolveTenantAsync(string slug, CancellationToken cancellationToken)
    {
        using var response = await httpClient.GetAsync($"/internal/tenants/by-slug/{Uri.EscapeDataString(slug)}", cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<TenantLookup>(cancellationToken);
    }

    public async Task<MembershipLookup?> ResolveMembershipAsync(Guid tenantId, Guid actorId, CancellationToken cancellationToken)
    {
        using var response = await httpClient.GetAsync($"/internal/tenants/{tenantId}/memberships/{actorId}", cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<MembershipLookup>(cancellationToken);
    }
}
