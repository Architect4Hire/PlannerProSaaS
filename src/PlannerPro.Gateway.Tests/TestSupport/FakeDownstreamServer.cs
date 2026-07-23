using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;

namespace PlannerPro.Gateway.Tests.TestSupport;

/// <summary>
/// A tiny second, real Kestrel host standing in for a proxied destination (e.g. "access"). Echoes back
/// every header it received as JSON, so a test can assert what the gateway's YARP proxy actually sent
/// downstream — the only way to prove header stripping/projection survives real proxying rather than
/// just the gateway's own view of the request before <c>MapReverseProxy()</c> runs.
/// </summary>
public sealed class FakeDownstreamServer : IAsyncDisposable
{
    private readonly WebApplication _app;

    public string Address { get; }

    private FakeDownstreamServer(WebApplication app, string address)
    {
        _app = app;
        Address = address;
    }

    public static async Task<FakeDownstreamServer> StartAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        var app = builder.Build();

        app.Map("/{**catch-all}", (HttpContext context) =>
        {
            var headers = context.Request.Headers.ToDictionary(h => h.Key, h => h.Value.ToString());
            return Results.Ok(headers);
        });

        await app.StartAsync();
        return new FakeDownstreamServer(app, app.Urls.First());
    }

    public async ValueTask DisposeAsync()
    {
        await _app.StopAsync();
        await _app.DisposeAsync();
    }
}
