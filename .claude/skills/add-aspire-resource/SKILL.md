---
name: add-aspire-resource
description: >
  Declare a new locally-orchestrated resource in the PlannerPro AppHost — a per-service database, a
  Service Bus topic/subscription, a cache, a blob container, or a whole new service — wiring it with
  the model (WithReference / WaitFor) and no hardcoded addresses. Use when asked to "add a database
  for X", "add a topic for the Y event", "add a cache to Z", or "stand up a new service". Keeps the
  AppHost declarative and local-first, and treats a new service as an architectural decision.
---

# Add an Aspire resource

Everything the system runs is declared **once, in `PlannerPro.AppHost`**. Nothing outside the AppHost
invents infrastructure, and nothing hardcodes an address. Read
[`.claude/rules/aspire.md`](../../rules/aspire.md) first; for messaging resources also
[`messaging.md`](../../rules/messaging.md), and for a new service also
[`backend.md`](../../rules/backend.md) and [`tenancy.md`](../../rules/tenancy.md).

**Local-first is the invariant.** Backing resources are local containers. An *emulator-backed* Azure
resource is in bounds because it is a local container — `AddAzureServiceBus(...).RunAsEmulator(...)`,
`AddAzureStorage(...).RunAsEmulator(...)`. A resource needing a real subscription (`AsExisting`, real
provisioning) is **not**; stop and ask.

## Pick the resource type

| You need… | Add in AppHost | Referenced by | Consumed via |
|---|---|---|---|
| A database for a service | `pg.AddDatabase("<svc>db")` on the shared Postgres server | that one service host (`WithReference` + `WaitFor`) | Aspire Npgsql integration → `<Service>DbContext` |
| A topic/subscription for an event | a topic + per-consumer subscription on `servicebus` | publisher + each consuming host | `AddAzureServiceBusClient` (already wired) |
| A blob container | on the Azurite `storage` resource | the owning service only | Aspire Blob client integration |
| A cache for a hot read | `AddRedis("cache")` | the service(s) that cache | Aspire Redis integration → facade cache |
| **A whole new service** | `AddProject<Projects.PlannerPro_X>("x")` + its own database | the gateway (route) | — **stop and ask first** |

## The scope boundary

A database, topic, container or cache is wiring. **A new service is not** — it's a new bounded
context, a new database, a new set of events, and a new surface for tenant leakage. Adding one is an
architectural decision: propose it, name the bounded context it owns and what it stops another service
from owning, and wait. Right-sizing is the point of this codebase.

## Steps

1. **Declare it in the AppHost** near related resources, with a comment saying *why* it exists.
2. **Wire it with the model.** `WithReference(...)` for consumers, `WaitFor(...)` for ordering. Never
   a connection string, namespace, blob endpoint, or `localhost:port` in consuming code.
3. **One database per service, always.** Never wire two services to the same database. Tenants share
   a database *within* a service via `TenantId` — that's the tenancy model, not an exception to this
   rule.
4. **Add the client integration** in the consuming service, keyed to the AppHost resource name. That
   name is the contract between the two files — keep them in step.
5. **If it's a new service:** it needs the full pattern — thin host, `.Core` stack over the Shared
   base context, `Add<Service>Core()`, `ITenantScoped` entities, inbox-idempotent consumers, a gateway
   route (unless internal-only), and its own cross-tenant tests. Do not stop at the AppHost line.
6. **Verify by starting the system.** `aspire run`, confirm the resource is healthy on the dashboard
   and dependents still start. Note anything you couldn't verify.

## Checklist before done
- [ ] Declared in the AppHost with a comment explaining why
- [ ] Wired with `WithReference` / `WaitFor`; **no** connection string or `host:port` in code
- [ ] One database per service preserved; no shared store
- [ ] Client integration added, keyed to the same resource name
- [ ] Local container only — nothing needing a real subscription
- [ ] A new *service* was proposed and approved, not assumed — and if added, got the full pattern
- [ ] `aspire run` verified; README updated
