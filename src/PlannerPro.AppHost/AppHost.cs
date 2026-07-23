var builder = DistributedApplication.CreateBuilder(args);

var sql = builder.AddSqlServer("sql")
    .WithLifetime(ContainerLifetime.Persistent);

var accessDb = sql.AddDatabase("accessdb");
var portfolioDb = sql.AddDatabase("portfoliodb");

var serviceBus = builder.AddAzureServiceBus("servicebus")
    .RunAsEmulator();

// Access is the only publisher today; "access-events" is its one outbox topic (Subject
// differentiates event type per `.claude/rules/messaging.md`). Portfolio's subscription is the first
// real consumer of the loop this AppHost's own comments anticipated. Named "portfolio-events", not
// "portfolio" — Aspire resource names are a single flat namespace, and "portfolio" is already taken
// by the Portfolio project resource below.
var accessEventsTopic = serviceBus.AddServiceBusTopic("access-events");
accessEventsTopic.AddServiceBusSubscription("portfolio-events");

var storage = builder.AddAzureStorage("storage")
    .RunAsEmulator();

// Blob-SERVICE-level resource (pairs with AddAzureBlobServiceClient client-side) and the specific
// container as its own first-class, auto-provisioned resource — see the remarks in
// PlannerPro.Access/Program.cs and PlannerPro.Gateway/Program.cs for why both the Gateway and Access
// need the same Data Protection key ring (ADR-0021: Access issues the auth cookie, the Gateway
// validates it on every later request).
var blobs = storage.AddBlobs("blobs");
var dataProtectionKeys = storage.AddBlobContainer("dataprotection-keys");

// Access now publishes TenantProvisioned through its outbox dispatcher — signup is the first event
// this system's own comments anticipated wiring.
var access = builder.AddProject<Projects.PlannerPro_Access>("access")
    .WithReference(accessDb).WaitFor(accessDb)
    .WithReference(blobs).WaitFor(dataProtectionKeys)
    .WithReference(serviceBus).WaitFor(serviceBus);

// Portfolio has no public HTTP surface in this slice — only a TenantProvisioned consumer — so it's
// not referenced by the gateway. Add that reference (and a YARP cluster/route) when it ships an
// endpoint, not before.
var portfolio = builder.AddProject<Projects.PlannerPro_Portfolio>("portfolio")
    .WithReference(portfolioDb).WaitFor(portfolioDb)
    .WithReference(serviceBus).WaitFor(serviceBus);

var gateway = builder.AddProject<Projects.PlannerPro_Gateway>("gateway")
    .WithReference(blobs).WaitFor(dataProtectionKeys)
    .WithReference(access).WaitFor(access);

var web = builder.AddJavaScriptApp("web", "../web", "start")
    .WithReference(gateway)
    .WaitFor(gateway);

builder.Build().Run();
