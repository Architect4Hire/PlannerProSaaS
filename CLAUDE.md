# PlannerPro

*Project memory, written as a SCRUB prompt — Scope, Constraints, Restrictions, Usage, Behavior. Loaded every session. Every new rule has one obvious home, and every misstep is diagnosable by section.*

## Scope

- PlannerPro is a **multi-tenant, white-label SaaS** for planning and tracking two-week sprints across parallel software projects — built as **event-driven microservices** on **Aspire + ASP.NET Core + Angular**, developed with Claude Code. It does two things at once: it's a genuinely good product, and a public demonstration of running a *tenant-isolated* multi-service stack agentically. The reusable toolkit lives in `.claude/`.

- The product hierarchy is **Tenant → Client → Project**. A tenant is a customer organization; clients are who *they* serve; projects hang off a client. Each sprint gives every project one goal, a status, and tasks carrying Fibonacci effort points, so an overloaded sprint is visible before it's lived through. On top of that sit a program **roadmap**, per-person **capacity** planning, **plan limits**, and per-tenant **branding**.

- **In bounds:** the eight bounded services, the gateway, the shared mechanism library, the event contracts, the Angular app, and the `.claude/` toolkit that builds them.

- **Out of bounds (don't build unprompted):** a new service, a new bus, a new shared library, or a synchronous service-to-service call. Adding a *service* is an architectural decision, not a feature — propose it, don't scaffold it. "Shared" means **cross-cutting mechanism and integration-event contracts only**; it never means shared domain code and never means a shared database. Also out of scope by prior decision: custom domains, SSO/SAML, live Stripe checkout, per-tenant data residency, a public API, and real-time collaboration. Stripe *columns* exist and stay null.

## Constraints

**Stack**

- **Orchestration:** Aspire 13 (AppHost + ServiceDefaults) on .NET 10
- **Services — two projects each, one bounded context each.** A service is a **thin host** (`PlannerPro.<Service>`, ASP.NET Core Web API: entry points + composition root only) plus a **class library** (`PlannerPro.<Service>.Core`) holding the whole **facade → business → data layer → repository** stack and its models/validators/mappers. The host references its own `.Core`; nothing else. EF Core / Npgsql lives in `.Core`.
  - `PlannerPro.Access` (+`.Core`) — **global** accounts + token issuance, and **tenant-scoped** tenants, settings, branding, memberships, invitations. Owns `accessdb`. Publishes `TenantProvisioned`, `TenantStatusChanged`, `TenantSettingsChanged`, `TenantBrandingChanged`, `MemberInvited`, `InvitationAccepted`, `MembershipChanged`.
  - `PlannerPro.Portfolio` (+`.Core`) — clients, teams, team members, projects. Owns `portfoliodb`. Publishes `ClientCreated`, `ClientArchived`, `TeamChanged`, `ProjectCreated`, `ProjectArchived`.
  - `PlannerPro.Planning` (+`.Core`) — sprints, sprint goals, tasks, capacity. The product core. Owns `planningdb`. Consumes `ProjectCreated`/`ProjectArchived`, `TenantSettingsChanged`. Publishes `SprintGoalSet`, `TaskChanged`, `CapacitySet`.
  - `PlannerPro.Roadmap` (+`.Core`) — the coarse program/Gantt view. Owns `roadmapdb`. Consumes `ProjectCreated`/`ProjectArchived`.
  - `PlannerPro.Files` (+`.Core`) — task attachments and tenant assets (logo, favicon) over Azurite. Owns `filesdb`. Publishes `AttachmentUploaded`, `AttachmentDeleted` (each carrying byte size).
  - `PlannerPro.Billing` (+`.Core`) — plans, limits, usage counters, trial/suspension lifecycle. Owns `billingdb`. Consumes the countable events; publishes `TenantQuotaChanged`, `TenantStatusChanged`, `TrialExpiring`.
  - `PlannerPro.Audit` (+`.Core`) — append-only support trail, fed by the bus. Owns `auditdb`. Consumes everything; publishes nothing.
  - `PlannerPro.Notifications` (+`.Core`) — consumes events and sends email; no public HTTP surface. Owns `notificationsdb`.
- **Edge:** `src/PlannerPro.Gateway/` — YARP reverse proxy. The **only** public entry point; the Angular app talks to nothing else. It also **resolves tenancy** (see below).
- **Shared cross-cutting code:** `src/PlannerPro.Shared/` — the library every `.Core` builds on: the base `DbContext` (owns `OutboxMessages` / `InboxMessages`, and applies the tenant query filter to every `ITenantScoped` entity), the base repository + `ExecuteInTransactionAsync`, `IOutbox` / `IInbox`, the outbox **dispatcher** and Service Bus **processor host**, `ITenantContext` + the tenant `SaveChanges` interceptor, the global exception handler, the cache abstraction, and the shared error shape. Cross-cutting *mechanism* only — no service's domain, business, or data ever lives here.
- **Shared contracts:** `src/PlannerPro.Contracts/` — integration-event records (each an `IIntegrationEvent` carrying `Id`, **`TenantId`**, `CorrelationId`, `CausationId`, and actor). No domain logic, no EF. It's a leaf: references nothing, referenced by everything.
- **Messaging:** **Azure Service Bus** for integration events, run locally as an **emulator** container via Aspire. Reliability is a **hand-rolled transactional outbox** in `PlannerPro.Shared` (per-service tables) — the event is written to that service's own `OutboxMessages` table in the *same transaction* as the domain write, and a background dispatcher relays it afterward. No MassTransit, no third-party outbox.
- **Frontend:** Angular 22 — **zoneless, signal-first**, standalone components, strict TS, `httpResource` for reads — in `src/web/`, run via `AddJavaScriptApp`, calling **only** the gateway.
- **Data/infra (local containers via Aspire):** one PostgreSQL server with a database *per service*, the Azure Service Bus emulator, Azurite for blobs, and Redis where a service needs a cache. No cloud spend.

**Layout**

```
src/
├── PlannerPro.AppHost/          # Aspire orchestrator — declares every resource + wiring
├── PlannerPro.ServiceDefaults/  # telemetry, health, resilience, discovery
├── PlannerPro.Contracts/        # integration-event records (leaf — references nothing)
├── PlannerPro.Shared/           # cross-cutting MECHANISM: base DbContext (+ tenant filter), base repo
│                                #   + ExecuteInTransactionAsync, IOutbox/IInbox, dispatcher + processor
│                                #   host, ITenantContext + tenant SaveChanges interceptor, exception
│                                #   handler, cache abstraction, error shape
├── PlannerPro.Gateway/          # YARP — the ONLY public door; resolves + projects tenancy
│
│   # each service = a thin host + a facade→repository library (shown for Planning; same for the rest)
├── PlannerPro.Planning/         # HOST: Controllers/, Consumers/, Program.cs (composition root)
├── PlannerPro.Planning.Core/    # LIBRARY: Facade/ Business/ Data/ Managers/  ── planningdb
├── PlannerPro.Access/    + .Core/   ── accessdb      (mixed: global users + tenant-scoped rest)
├── PlannerPro.Portfolio/ + .Core/   ── portfoliodb
├── PlannerPro.Roadmap/   + .Core/   ── roadmapdb
├── PlannerPro.Files/     + .Core/   ── filesdb       (+ Azurite blobs)
├── PlannerPro.Billing/   + .Core/   ── billingdb
├── PlannerPro.Audit/     + .Core/   ── auditdb       (append-only)
├── PlannerPro.Notifications/ + .Core/ ── notificationsdb (no public HTTP)
└── web/                         # Angular app (AddJavaScriptApp target)
```

**Reference direction is one-way and acyclic** — `Contracts` ← `Shared` ← `<Service>.Core` ← `<Service>` (host) ← `AppHost`; hosts and the gateway also reference `ServiceDefaults`. A host references *its own* `.Core` and never another service's. If a reference would point the other way, or sideways between two services, the design is wrong.

**Tenancy — the spine of this system**

- **`TenantId` is a `Guid`,** never an `int`, never guessable, never taken from a request body.
- **Every tenant-scoped entity implements `ITenantScoped`** (`Guid TenantId { get; set; }`), and the base `DbContext` applies a global query filter to every one of them automatically. A new entity that isn't `ITenantScoped` is a deliberate, defended decision.
- **Tenancy is path-based:** the app routes are `/t/{slug}/…`; the API routes are `/api/t/{slug}/…`. Single origin, so cookie + antiforgery keep working with no CORS.
- **The gateway resolves tenancy once,** at the edge: slug → tenant → the caller's membership. It **strips any client-supplied** tenant, actor, or correlation header and projects its own trusted set inward. A non-member gets **404, never 403** — never confirm a tenant exists to someone outside it.
- **A consumer has no HTTP request.** The Service Bus processor host establishes tenant scope from the **event's `TenantId`** before invoking a consumer, and every integration event carries one. This is the single most breakable mechanism in the system.
- **Defense is layered, deliberately:** query filters, the `SaveChanges` interceptor (`CrossTenantWriteException` on a mismatched update/delete), gateway resolution, role filters, and tests. No single mistake should leak data.
- **`accessdb` is the one mixed store.** Identity tables are **global and deliberately unfiltered** — one account per email platform-wide, tenancy expressed via `TenantMembership`. Everything else in that database is filtered like any other service.

**Architecture conventions** — area detail auto-loads from `.claude/rules/` (`tenancy.md`, `aspire.md`, `backend.md`, `messaging.md`, `gateway.md`, `billing.md`, `audit.md`, `frontend.md`). The essentials:

- **Aspire:** every resource — each database, the Service Bus emulator, Azurite, every host, the gateway, the web app — is declared in the AppHost and wired with `WithReference` / `WaitFor`, never strings.
- **Per-service internals:** the host holds only entry points and the composition root; the whole facade → business → data layer → repository stack lives in `<Service>.Core`. Only ViewModels in, only ServiceModels out; never expose EF entities; everything async; validate at the edge.
- **Between services:** talk over **Service Bus** (integration events, published via each service's outbox), never by reaching into another service's database and never by a chatty synchronous call. Duplicate the little reference data you need; don't couple.
- **Limits are enforced locally against a replicated quota.** Billing owns plans and counters, but a service enforcing a limit reads its **own local quota snapshot**, kept current by `TenantQuotaChanged`. That's eventual consistency with a small, deliberate overshoot window — Billing reconciles and is the authority.
- **Frontend:** zoneless and signal-first, typed models mirroring ServiceModels, HTTP through typed services to the **gateway** base URL only, tenant slug injected by one interceptor.

**Canonical commands** (use these verbatim)

- Whole system: `aspire run` · add a resource package: `aspire add <resource>`
- A service: `dotnet test` runs that service's test project. Migrations live in `<Service>.Core` but need the host as startup project — run from the host folder: `dotnet ef migrations add <n> --project ../PlannerPro.<Service>.Core --startup-project . --context <Service>DbContext`, then `dotnet ef database update` with the same arguments.
- Frontend (`src/web/`): `npm install` · `ng test` · `ng build`

## Restrictions

- **No shared database, ever.** A service reads and writes **only** its own database. Needing another service's data is a signal to consume its event and keep a local copy, or to route a query through the gateway — never a second connection string. If you're tempted to add one, stop and say so.
- **Never trust a tenant identifier from the client.** `TenantId` comes from the gateway's projected header on an HTTP path, or from the event envelope on a bus path. A tenant id read from a request body, a query string, or a JWT claim the gateway didn't mint is a security bug, not a shortcut.
- **404, never 403, for a tenant the caller can't access.** A 403 confirms the tenant exists. So does a distinct error message, a different response time on an obvious path, or a validation error that names something inside the tenant. Same rule for a resource id belonging to another tenant.
- **Never `FindAsync` on a tenant-scoped entity.** `Find`/`FindAsync` return tracked entities **without applying query filters** — the exact hole this architecture exists to close. Use `FirstOrDefaultAsync` (or `SingleOrDefaultAsync`) always. This is not a style preference; it is the most likely way this system leaks data.
- **Never `IgnoreQueryFilters()` outside a system context.** Migration, seeding, and the platform-admin surface run under an explicit bypass context and nowhere else. An `IgnoreQueryFilters()` in a facade, business, or repository method serving a normal request is a defect.
- **Every integration event carries `TenantId`.** An event without one cannot be consumed safely, because the consumer has no other way to establish scope. Publishing one is a bug even if the current consumer happens not to need it.
- **Consumers are idempotent, via the inbox.** Delivery is **at least once**, so every consumer dedupes on message id in the same transaction as its side effect. A handler that isn't safe to run twice is a bug, not an edge case.
- **Publish through the outbox, in the same transaction as the write.** Never send to Service Bus inline from business or data code. The dispatcher is the *only* thing that sends.
- **The gateway is the only public door,** and the only place tenancy is resolved. Services are never exposed to the browser, and a service never re-resolves a slug.
- **`Contracts` holds events and nothing else.** `PlannerPro.Shared` holds cross-cutting **mechanism** only. If something feels shared but is really one service's logic, it belongs in that service's `.Core`.
- **The host stays thin.** Entry points and composition root only. Logic creeping into a controller or `Program.cs` is in the wrong project.
- **Wire through Aspire.** No hardcoded connection strings, broker addresses, blob endpoints, or `localhost:port`. **One sanctioned exception:** the gateway's YARP `Clusters` may name services by their Aspire resource name (e.g. `http://planning`), because service discovery resolves those names.
- **No credentials in the repo, ever.** Seed and signing values come from user-secrets in dev and environment variables in production. `appsettings.json` keeps them blank by design — don't "helpfully" fill them in.
- **Blob names are tenant-prefixed** — `{tenantId}/{taskId}/{guid}.ext`. An unprefixed name means a guessed id yields another tenant's file and storage accounting is impossible.
- **Suspended tenants go read-only, not locked out.** Reads and export keep working; writes are refused. Don't implement suspension as a login block.
- Don't put business logic in the AppHost or the gateway; both stay declarative. Don't run `ng serve` by hand — Aspire launches the client. Don't hand-edit generated EF migrations except to review them. Don't commit `bin/`, `obj/`, `node_modules/`, `.claude/settings.local.json`, or any secret.

## Usage

- The world is **local**: Aspire orchestrates every service, the gateway, the Angular app, and all backing resources (Postgres with a database per service, the Service Bus emulator, Azurite, Redis) as local containers — no cloud dependencies. The dashboard is the front door for logs, traces, and health; a request should be followable from the gateway through the owning service and onto the bus.
- **`TenantId` is a tag on every span and log scope.** Debugging a multi-tenant system without it is guesswork.
- The Angular app is the primary consumer, and it consumes **the gateway** — keep that contract stable; the service boundaries behind it can move.
- Available tooling in `.claude/`: rules auto-load from `.claude/rules/`; task skills live in `.claude/skills/` (`add-endpoint`, `add-tenant-scoped-entity`, `add-component`, `add-aspire-resource`, `add-audit-event`, `trace-a-request`); subagents are available but run **read-only**.
- The scaffolding sequence is `docs/prompts/scrub-prompts.md`. Run Part 1 in order; don't fan out to eight services until one service and the full tenant-aware event loop work end to end.

## Behavior

- Plan before any change touching more than one file — and **always** name which service(s) it lands in before touching code. A change spanning two services is a design conversation first.
- **Say what tenancy story a change has, every time.** Which entity is `ITenantScoped`, where `TenantId` comes from on each path, and what a caller from another tenant sees. If the answer is "it doesn't need one," say why out loud rather than leaving it unstated.
- Use the matching skill in `.claude/skills/` instead of freelancing.
- Run the relevant service's tests before calling a task done; for anything crossing the bus, run both the publisher's and the consumer's. Run `@tenant-isolation-auditor` on any change that adds an entity, an endpoint, a consumer, or a query.
- Report honestly. If a migration wasn't run against realistic data, if a cross-tenant case wasn't tested, or if a decision here turns out to be wrong once you're in the code — **say so** rather than silently deviating.
- Make edits in the main session so I can approve them — subagents stay read-only.
