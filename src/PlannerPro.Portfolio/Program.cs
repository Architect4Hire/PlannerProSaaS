using PlannerPro.Contracts;
using PlannerPro.Portfolio.Consumers;
using PlannerPro.Portfolio.Core;
using PlannerPro.Shared.Messaging;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddPortfolioCore(builder.Configuration);

// Portfolio has no public HTTP surface yet in this slice — only a bus consumer, so no
// AddControllers()/authentication/authorization wiring (nothing would use it; add it when Portfolio
// ships its first endpoint, per `.claude/rules/backend.md`).
builder.AddAzureServiceBusClient("servicebus");
builder.Services.AddIntegrationEventConsumer<TenantProvisioned, TenantProvisionedConsumer>();
builder.Services.AddServiceBusProcessorHost("access-events", "portfolio-events");

var app = builder.Build();

app.MapDefaultEndpoints();

app.Run();

// Exposed for WebApplicationFactory<Program> in PlannerPro.Portfolio.Tests.
public partial class Program;
