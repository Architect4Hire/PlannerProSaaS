using Azure.Storage.Blobs;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using PlannerPro.Gateway.Authentication;
using PlannerPro.Gateway.Http;
using PlannerPro.Gateway.Middleware;
using PlannerPro.Gateway.Tenancy;
using PlannerPro.Shared;
using PlannerPro.Shared.Authentication;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddSharedExceptionHandler();

// The "blobs" connection name is the Aspire blob-SERVICE-level resource (storage.AddBlobs("blobs")
// in the AppHost). This gateway validates the auth cookie PlannerPro.Access issues on login
// (ADR-0021); both must decrypt against the same Data Protection key ring, hence the shared blob
// container instead of each process's own (ephemeral, per-instance) default key store.
builder.AddAzureBlobServiceClient("blobs");
builder.Services.AddDataProtection()
    .SetApplicationName(AuthCookieScheme.DataProtectionApplicationName)
    .PersistKeysToAzureBlobStorage(sp =>
        sp.GetRequiredService<BlobServiceClient>()
            .GetBlobContainerClient("dataprotection-keys")
            .GetBlobClient("keys.xml"));

builder.Services.AddGatewayCookieAuthentication(builder.Environment);
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("RequireAuthenticatedCaller", policy => policy.RequireAuthenticatedUser());

    // A coarse, uniform gate for the whole /api/admin/* prefix (every admin route needs the same
    // platform-admin check, unlike per-endpoint tenant roles, so doing it once here is consistent
    // with "one implementation of the most security-sensitive check" rather than trusting every
    // future admin endpoint in Access to remember it). This is a 403 today, not the 404-never-403
    // treatment the rest of the platform surface eventually needs — that refinement (a custom
    // authorization result handler so a non-admin gets the same indistinguishable 404 a non-member
    // gets) is deliberately left to the prompt that builds the actual admin surface.
    options.AddPolicy("RequirePlatformAdmin", policy => policy
        .RequireAuthenticatedUser()
        .RequireClaim("IsPlatformAdmin", "true"));
});

builder.Services.AddMemoryCache();
builder.Services.Configure<TenantDirectoryOptions>(builder.Configuration.GetSection("Tenancy"));
builder.Services.AddHttpClient<AccessTenantDirectory>(client => client.BaseAddress = new Uri("http://access"));

// Singleton, not Scoped: TenantResolutionMiddleware is a conventional middleware, constructed once
// via ActivatorUtilities against the ROOT service provider on the first request that reaches it — not
// per-request. A Scoped registration here throws "Cannot resolve scoped service ... from root
// provider" under ValidateScopes (on by default in Development, i.e. exactly how Aspire runs this
// locally). Nothing in this chain needs per-request state: IMemoryCache is already a singleton, and a
// long-lived typed HttpClient instance is the supported pattern (the underlying handler is separately
// pooled/rotated by IHttpClientFactory regardless of how long this wrapper is held).
builder.Services.AddSingleton<ITenantDirectory>(sp => new CachedTenantDirectory(
    sp.GetRequiredService<AccessTenantDirectory>(),
    sp.GetRequiredService<IMemoryCache>(),
    sp.GetRequiredService<IOptions<TenantDirectoryOptions>>()));

builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"))
    .AddServiceDiscoveryDestinationResolver();

var app = builder.Build();

app.UseExceptionHandler();

app.UseAuthentication();
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<TrustedHeaderStrippingMiddleware>();
app.UseMiddleware<ActorProjectionMiddleware>();
app.UseAuthorization();
app.UseMiddleware<TenantResolutionMiddleware>();

app.MapDefaultEndpoints();
app.MapGatewayLocalEndpoints();
app.MapReverseProxy();

app.Run();

// Exposed for WebApplicationFactory<Program> in PlannerPro.Gateway.Tests.
public partial class Program;
