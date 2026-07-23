---
paths:
  - src/PlannerPro.AppHost/**
  - src/PlannerPro.ServiceDefaults/**
---
# Aspire rules — AppHost & ServiceDefaults

The AppHost is the single source of truth for the application model. Keep it declarative.

- **Declare every resource here.** The Postgres server and each per-service database, the Azure
  Service Bus emulator, Azurite, Redis, every service host, the gateway, and the Angular app are all
  added in the AppHost — e.g. `AddPostgres("pg")` then `.AddDatabase("planningdb")` /
  `.AddDatabase("accessdb")` (one database per service),
  `AddAzureServiceBus("servicebus").RunAsEmulator(...)`,
  `AddAzureStorage("storage").RunAsEmulator(...)`,
  `AddProject<Projects.PlannerPro_Planning>("planning")`, and
  `AddJavaScriptApp("web", "../web", "start")`. Nothing outside the AppHost invents infrastructure.
- **Database per service — no sharing.** Each service gets its own database resource and references
  only that one. Never wire two services to the same database; cross-service data moves over Service
  Bus. Tenants share a database *within* a service, separated by `TenantId` — that's the tenancy
  model, not a shortcut (see `.claude/rules/tenancy.md`).
- **Local-first.** Backing resources run as local containers — no cloud resources in this showcase.
  An *emulator-backed* Azure resource is in bounds because it is a local container:
  `AddAzureServiceBus(...).RunAsEmulator(...)` and `AddAzureStorage(...).RunAsEmulator(...)` run
  exactly as `AddPostgres` runs Postgres. The test is where it runs, not what the API is called.
  What stays out is anything needing a real subscription — `AsExisting`, or provisioned for real.
- **Wire with the model, not with strings.** Connect services with `WithReference(...)` (their
  database, the Service Bus, storage, the cache) and order startup with `WaitFor(...)`. Never
  hardcode a connection string, a Service Bus namespace, a blob endpoint, or `localhost:port`.
- **The gateway is a first-class resource.** It's the only public entry point; give services stable
  Aspire resource names (`access`, `planning`, `portfolio`, …) because the gateway resolves routes by
  those names through service discovery.
- **Cross-cutting config lives in ServiceDefaults.** OpenTelemetry, health checks, resilience, and
  service discovery are configured once; every host and the gateway call `AddServiceDefaults()`.
  **`TenantId` is enriched onto spans and log scopes here** — one place, so every service gets it.
- **No business logic in the AppHost.** It orchestrates; it doesn't compute.
- **The Angular app is an `AddJavaScriptApp` resource.** Aspire runs it and injects the gateway
  endpoint — the frontend reads the gateway base URL from injected config, never a hardcoded value.

When adding a resource, use the `add-aspire-resource` skill. Verify exact API names
(`AddAzureServiceBus`, `RunAsEmulator`, the emulator's entity-config format, `AddAzureStorage`,
`AddJavaScriptApp`, client-integration methods, package names) against https://aspire.dev — these
move between versions.
