using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace PlannerPro.Gateway.Http;

/// <summary>Endpoints the gateway answers locally rather than proxying.</summary>
public static class GatewayEndpoints
{
    public static WebApplication MapGatewayLocalEndpoints(this WebApplication app)
    {
        app.MapGet("/api/ping", () => Results.Ok(new { status = "ok" }))
            .AllowAnonymous();

        return app;
    }
}
