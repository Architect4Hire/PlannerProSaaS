using Azure.Storage.Blobs;
using Microsoft.AspNetCore.DataProtection;
using PlannerPro.Access.Core;
using PlannerPro.Access.Core.Data;
using PlannerPro.Shared;
using PlannerPro.Shared.Authentication;
using PlannerPro.Shared.Messaging;
using PlannerPro.Shared.Tenancy;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddSharedExceptionHandler();
builder.Services.AddControllers();

// Registers AccessDbContext too — deliberately NOT via the Aspire.Microsoft.EntityFrameworkCore.SqlServer
// package; see the remarks on AddAccessCore for why that integration's unconditional DbContext pooling
// is incompatible with this system's per-request-scoped ITenantContext design.
builder.Services.AddAccessCore(builder.Configuration);

// Access now publishes (the signup endpoint's TenantProvisioned) — one topic per publishing service,
// Subject differentiates event type (`.claude/rules/messaging.md`). The dispatcher is the only sender;
// nothing else in this service ever touches ServiceBusClient directly.
builder.AddAzureServiceBusClient("servicebus");
builder.Services.AddOutboxDispatcher<AccessDbContext>("access-events");

// The "blobs" connection name is the Aspire blob-SERVICE-level resource (storage.AddBlobs("blobs")
// in the AppHost) — distinct from the "dataprotection-keys" AddBlobContainer resource, which exists
// purely so the container itself is a first-class, auto-provisioned Aspire resource. The
// BlobServiceClient this registers is account-level; the container is selected explicitly below.
builder.AddAzureBlobServiceClient("blobs");

// Access is the only service that ever issues the auth cookie (ADR-0021); the Gateway validates it on
// every later request. Both must decrypt against the SAME Data Protection key ring, hence the shared
// blob container instead of each process's own (ephemeral, per-instance) default key store.
builder.Services.AddDataProtection()
    .SetApplicationName(AuthCookieScheme.DataProtectionApplicationName)
    .PersistKeysToAzureBlobStorage(sp =>
        sp.GetRequiredService<BlobServiceClient>()
            .GetBlobContainerClient("dataprotection-keys")
            .GetBlobClient("keys.xml"));

builder.Services.AddAuthCookieScheme(builder.Environment);
builder.Services.AddAuthorization();

var app = builder.Build();

app.UseExceptionHandler();

app.UseAuthentication();
// Before UseAuthorization(): a future RequireTenantRole policy resolves during UseAuthorization and
// needs ITenantContext already populated, or it always sees IsResolved == false (`.claude/rules/tenancy.md`
// Layer 5). No tenant-scoped endpoint exists yet, so this is currently inert — but wrong-order-here is
// exactly the kind of mistake this being "the template" would otherwise propagate to every future service.
app.UseMiddleware<TenantContextMiddleware>();
app.UseAuthorization();

app.MapDefaultEndpoints();
app.MapControllers();

app.Run();

// Exposed for WebApplicationFactory<Program> in PlannerPro.Access.Tests.
public partial class Program;
