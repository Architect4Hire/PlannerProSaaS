var builder = DistributedApplication.CreateBuilder(args);

var sql = builder.AddSqlServer("sql")
    .WithLifetime(ContainerLifetime.Persistent);

var accessDb = sql.AddDatabase("accessdb");

var serviceBus = builder.AddAzureServiceBus("servicebus")
    .RunAsEmulator();

var storage = builder.AddAzureStorage("storage")
    .RunAsEmulator();

// Blob-SERVICE-level resource (pairs with AddAzureBlobServiceClient client-side) and the specific
// container as its own first-class, auto-provisioned resource — see the remarks in
// PlannerPro.Access/Program.cs and PlannerPro.Gateway/Program.cs for why both the Gateway and Access
// need the same Data Protection key ring (ADR-0021: Access issues the auth cookie, the Gateway
// validates it on every later request).
var blobs = storage.AddBlobs("blobs");
var dataProtectionKeys = storage.AddBlobContainer("dataprotection-keys");

// Not yet wired to serviceBus: Access publishes no events until the tenant-provisioning endpoint
// (next in the scaffolding sequence) actually needs the outbox dispatcher — adding it now would be
// dead wiring.
var access = builder.AddProject<Projects.PlannerPro_Access>("access")
    .WithReference(accessDb).WaitFor(accessDb)
    .WithReference(blobs).WaitFor(dataProtectionKeys);

var gateway = builder.AddProject<Projects.PlannerPro_Gateway>("gateway")
    .WithReference(blobs).WaitFor(dataProtectionKeys)
    .WithReference(access).WaitFor(access);

var web = builder.AddJavaScriptApp("web", "../web", "start")
    .WithReference(gateway)
    .WaitFor(gateway);

builder.Build().Run();
