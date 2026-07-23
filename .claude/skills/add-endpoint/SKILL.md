---
name: add-endpoint
description: >
  Add a REST endpoint (or an event-driven consumer) to one PlannerPro service, using the layered
  controller → facade → business → data layer → repository architecture inside the service, plus the
  cross-service and multi-tenant seams this system needs. Use whenever creating or extending routes —
  e.g. "add an endpoint to set a sprint goal", "let admins archive a client", "recalculate capacity
  when a member is removed". Produces the controller action (or consumer), view models, service
  models, facade, business, data layer, repository, validators, mappers, integration-event contract,
  gateway route, DI wiring, and tests that match this repo's conventions.
---

# Add an endpoint

First decide **which service owns this**, then build inside it. The owner is the service whose
database holds the data being changed or read — a sprint goal lives in **Planning**, a client in
**Portfolio**, a membership in **Access**, a plan limit in **Billing**. If the work reads or writes
two services' data, it is **not one endpoint**: it's an endpoint in one service plus an **integration
event** the other reacts to. Never open a second database. When in doubt about ownership, stop and ask.

Once the owner is fixed, remember the service is **two projects**: a **thin host**
(`PlannerPro.<Service>`) holding only entry points and the composition root, and a **library**
(`PlannerPro.<Service>.Core`) holding the whole facade→repository stack. Both build on
`PlannerPro.Shared`. Stop for review before running migrations.

## Before any code: state the tenancy story

Every endpoint in this system has one, and saying it out loud is the cheapest defect prevention
available. Answer these four, in the plan, before writing:

1. **Where does `TenantId` come from?** Gateway-projected header (HTTP) or event envelope (bus).
   There is no third answer. If you're reaching for a ViewModel field, stop.
2. **Which entities involved are `ITenantScoped`,** and are any deliberately not?
3. **What does a caller from another tenant see?** It must be a plain 404 — not 403, not a
   distinguishable message, not a validation error naming an internal field.
4. **What role does this require?** Owner / Admin / Member / Viewer, per the role matrix. Reads are
   generally Viewer+; editing goals and tasks is Member+; managing clients, teams, members, settings
   and branding is Admin+; changing plan, deleting the tenant and transferring ownership are Owner
   only.

## Target layout (host + Core library + Shared)

```
PlannerPro.<Service>/                   # HOST — thin: entry points + composition root only
├── Controllers/            # <Feature>Controller.cs — HTTP surface (ViewModel in, ServiceModel out)
├── Consumers/              # <Event>Consumer : IIntegrationEventConsumer<TEvent> — calls the SAME facade
└── Program.cs              # composition root: Add<Service>Core() + the Shared registration extensions

PlannerPro.<Service>.Core/              # LIBRARY — the whole facade → business → data → repository stack
├── Facade/                 # I<Feature>Facade  + <Feature>Facade   (validate VM + cache + return SM)
├── Business/               # I<Feature>Business + <Feature>Business (VM→domain, rules, build event, domain→SM)
├── Data/
│   ├── <Service>DbContext.cs   # derives from Shared's base context → inherits Outbox/Inbox + tenant filters
│   ├── I<Feature>DataLayer + <Feature>DataLayer   (compose data ops + enqueue outbox row via IOutbox)
│   └── I<Feature>Repository + <Feature>Repository (EF queries; ExecuteInTransactionAsync from base repo)
├── Managers/
│   ├── Validators/         # FluentValidation validators for the view models
│   ├── Models/
│   │   ├── ViewModels/     # inbound request types — the ONLY thing the controller binds
│   │   ├── ServiceModels/  # outbound response types — the ONLY thing the API returns
│   │   └── Domain/         # EF entities (ITenantScoped) + domain exceptions
│   └── Mappers/            # VM→domain, domain→ServiceModel, domain→integration event
├── Migrations/
└── <Service>CoreServiceCollectionExtensions.cs   # Add<Service>Core(): registers every layer + validators
```

The host references **its own** `.Core` and nothing else; `.Core` references `PlannerPro.Shared` and
`PlannerPro.Contracts`. Event **records** live in `Contracts`; the outbox/inbox/dispatcher/tenancy
**machinery** lives in `Shared` and is reused unchanged — most endpoints add an event *type*, never
new plumbing.

## The model types

| Type | Folder | Lives between | Who creates it |
|------|--------|---------------|----------------|
| **ViewModel** | `Managers/Models/ViewModels/` | client → controller → facade | model binder |
| **Domain** entity | `Managers/Models/Domain/` | business ↔ data ↔ EF | business (from the VM) / EF (on load) |
| **ServiceModel** | `Managers/Models/ServiceModels/` | business → facade → controller → client | business (from the entity) |
| **IntegrationEvent** | `PlannerPro.Contracts/` | this service → outbox → bus → other services | business (builds it) |

**A ViewModel never carries `TenantId`.** It arrives ambiently and the `SaveChanges` interceptor
stamps it. A ViewModel with a tenant id is a security bug, not a convenience.

## Steps

1. **Controller action** — bind the ViewModel, call the facade, return `ActionResult<ServiceModel>`.
   No logic. Apply the role filter (`RequireTenantRole(...)`).
2. **Facade** — validate the ViewModel (FluentValidation), own any ServiceModel caching, call
   business. No mapping, EF, or bus. If the operation enforces a plan limit, this is where the local
   quota snapshot is checked — return `402` with the machine-readable code (see `rules/billing.md`).
3. **Business** — map ViewModel → Domain, apply domain rules, **build** the integration event when the
   change warrants one (stamping `TenantId`, `CorrelationId`, `CausationId`, actor), map Domain →
   ServiceModel. No validation, cache, EF, or send.
4. **Data layer** — compose repository calls into the whole operation and enqueue the outbox row via
   `IOutbox` **inside the same transaction** as the domain write, using `ExecuteInTransactionAsync`.
5. **Repository** — EF queries only. **`FirstOrDefaultAsync`, never `FindAsync`** — `Find` bypasses
   the tenant query filter on tracked entities and is the most likely way this system leaks.
6. **Consumer (if event-driven)** — implement `IIntegrationEventConsumer<TEvent>` in the host, call
   the **same facade**, and dedupe via the inbox in the same transaction as the side effect. Do **not**
   resolve tenancy yourself; the processor host has already established scope from the envelope.
7. **Contract (if publishing)** — add the past-tense record to `PlannerPro.Contracts`, carrying only
   what a consumer needs plus the required envelope fields. Changing an existing event is a contract
   change affecting every consumer — list them and update them in the same change.
8. **Gateway route** — a client-facing endpoint needs one, named by Aspire resource name. No route
   means unreachable (fine for internal-only, as with Notifications).
9. **DI wiring** — register new layers in `Add<Service>Core()`; register the consumer in the host.
10. **Tests** — facade validation/cache branches, business rules, data-layer atomicity (rollback
    leaves neither the domain row nor the outbox row), consumer idempotency, consumer envelope scope,
    and **the cross-tenant case**: a caller from tenant B using tenant A's ids gets 404 and mutates
    nothing.

## Checklist before done

- [ ] Correct owning service; no second database, no synchronous cross-service call
- [ ] Tenancy story stated: source of `TenantId`, scoped entities, cross-tenant response, role
- [ ] `TenantId` absent from every ViewModel; never read from body, query or route by a service
- [ ] **No `FindAsync`**; no `IgnoreQueryFilters` outside a bypass context
- [ ] ViewModels in / ServiceModels out; no EF entity at the boundary
- [ ] Event published through the outbox in the same transaction, carrying the full envelope
- [ ] Consumer idempotent via the inbox; scope taken from the envelope, not resolved locally
- [ ] Role filter applied per the matrix; non-member and wrong-tenant both yield 404
- [ ] Gateway route added (or deliberately omitted for an internal endpoint)
- [ ] Tests pass, including the cross-tenant case; `@tenant-isolation-auditor` run and clean
