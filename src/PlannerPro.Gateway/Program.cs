using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using PlannerPro.Gateway.Authentication;
using PlannerPro.Gateway.Http;
using PlannerPro.Gateway.Middleware;
using PlannerPro.Gateway.Tenancy;
using PlannerPro.Shared;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddSharedExceptionHandler();

builder.Services.AddGatewayCookieAuthentication(builder.Environment);
builder.Services.AddAuthorization(options =>
    options.AddPolicy("RequireAuthenticatedCaller", policy => policy.RequireAuthenticatedUser()));

builder.Services.AddMemoryCache();
builder.Services.Configure<TenantDirectoryOptions>(builder.Configuration.GetSection("Tenancy"));
builder.Services.AddHttpClient<AccessTenantDirectory>(client => client.BaseAddress = new Uri("http://access"));
builder.Services.AddScoped<ITenantDirectory>(sp => new CachedTenantDirectory(
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
