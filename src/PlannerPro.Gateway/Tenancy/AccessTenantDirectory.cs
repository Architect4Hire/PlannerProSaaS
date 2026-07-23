using System.Net;

namespace PlannerPro.Gateway.Tenancy;

/// <summary>
/// The real <see cref="ITenantDirectory"/>, calling <c>PlannerPro.Access</c>'s internal resolution
/// endpoints over HTTP (service discovery target <c>http://access</c>, wired via
/// <c>AddServiceDefaults()</c>'s <c>ConfigureHttpClientDefaults</c> for discovery + resilience).
/// </summary>
/// <remarks>
/// <b>Access does not exist yet</b> (it ships in the next prompt in this project's scaffolding
/// sequence) and isn't registered as an Aspire resource, so these calls fail today — that is expected,
/// not a defect in this type. The endpoints below (<c>GET /internal/tenants/by-slug/{slug}</c>,
/// <c>GET /internal/tenants/{tenantId}/memberships/{actorId}</c>) are this gateway's expectation of
/// the contract Access will need to expose; they are not yet implemented on the other side.
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
