# PlannerPro — SCRUB Prompts

Prompts for driving Claude Code on PlannerPro (Aspire + ASP.NET Core microservices + Angular, multi-tenant SaaS), all wired to the skills, rules, and subagents in your `.claude/` folder.

Two parts:

- **Part 1 — Scaffolding:** a one-time sequence, run in order, to stand the system up. Because this is a *multi-tenant microservice* app, the sequence stands up the shared spine first, then the **tenancy mechanism**, then proves **one** service and the **whole tenant-aware event loop** end to end — and only then fans out.
- **Part 2 — Operational templates:** reusable prompts for the recurring, high-stakes moments the agent won't self-guard (features that cross the bus, migrations, refactors, isolation review, debugging). Fill in the blanks and go.

Once the system is scaffolded, most day-to-day work needs no bespoke prompt — your rules, skills, subagents, and hooks carry the structure. Reach for Part 2 only when a task is non-trivial or risky. When you reuse one two or three times, promote it to a skill so you stop needing the prompt at all.

## The reusable SCRUB skeleton

```
SCOPE:        what to build/change + which service/project it touches
CONSTRAINT:   the rules to honor (stack, conventions, plan-first)
RESTRICTION:  explicit "do NOT" guardrails
USAGE:        which skills / subagents / tools to use
BEHAVIOR:     how to proceed — plan, approve, small steps, test, report
```

## How to use these

- Run the Part 1 prompts **in order**, one at a time. Don't paste the whole file at once.
- Each assumes `CLAUDE.md` (repo root) and `.claude/` — rules (`tenancy.md`, `aspire.md`, `backend.md`, `messaging.md`, `gateway.md`, `billing.md`, `audit.md`, `frontend.md`), skills (`add-endpoint`, `add-tenant-scoped-entity`, `add-component`, `add-aspire-resource`, `add-audit-event`, `trace-a-request`), and subagents (`code-reviewer`, `test-gap-analyzer`, `api-contract-checker`, `tenant-isolation-auditor`) — are already in place.
- Every prompt asks Claude to **plan first and wait for approval** before editing. Read the plan before you say go; that's the biggest quality lever. In a multi-tenant microservice system it's also where you catch a boundary drawn in the wrong place, or a tenancy story left unstated, before any code exists.
- Use `/clear` between big steps to keep context lean; rules and skills reload on their own.
- **The golden rule of the sequence:** the tenancy mechanism (Prompts 2 and 3) and the isolation test suite (Prompt 8) come *before* the fan-out. A tenant leak replicated across eight services is eight times the work to fix, and you won't find it by reading code.

---

# Part 1 — Scaffolding (run once, in order)

## Prompt 0 — Solution skeleton + Aspire spine

```
SCOPE: Stand up the PlannerPro solution skeleton only (no domain, no endpoints, no tenancy). Create
an Aspire 13 solution on .NET 10 with these projects under src/: PlannerPro.AppHost (orchestrator),
PlannerPro.ServiceDefaults, PlannerPro.Contracts (class library — empty but for an IIntegrationEvent
marker carrying Guid Id, Guid TenantId, Guid CorrelationId, Guid? CausationId, and an actor id),
PlannerPro.Shared (class library — empty shell), and PlannerPro.Gateway (a YARP reverse proxy, no
routes yet). In the AppHost declare a local PostgreSQL server resource, the Azure Service Bus
emulator, and Azurite; register the gateway; register the Angular app in src/web as a JavaScript app
so Aspire launches it. Set the reference direction Contracts <- Shared; the gateway and
ServiceDefaults reference nothing app-specific.

CONSTRAINT: Follow .claude/rules/aspire.md and .claude/rules/gateway.md. All resources are local
containers. Put shared build settings in Directory.Build.props and use central package management in
Directory.Packages.props — do NOT set TargetFramework/Nullable per project and do NOT put versions on
PackageReferences. Verify exact Aspire commands, template names, and API surface (AddAzureServiceBus/
RunAsEmulator, AddAzureStorage/RunAsEmulator, AddJavaScriptApp, YARP wiring, package names) against
https://aspire.dev before running anything — do not guess.

RESTRICTION: Do NOT add any service host, DbContext, domain model, endpoint, messaging, or tenancy
code yet. Do NOT hardcode any connection string or localhost:port. Do NOT add real cloud/Azure
resources — emulators only. Do NOT let Contracts reference anything.

USAGE: Use the aspire CLI/templates. Use plan mode.

BEHAVIOR: First show me the plan: exact projects, references, AppHost wiring, and the commands you'll
run. Wait for my approval. Then scaffold, run `aspire run`, and tell me what the dashboard shows
(Postgres + Service Bus emulator + Azurite + gateway + web, all healthy).
```

## Prompt 1 — The shared spine: base persistence + outbox/inbox (PlannerPro.Shared)

```
SCOPE: Fill in PlannerPro.Shared with the cross-cutting persistence + messaging MECHANISM every
service reuses: a base DbContext exposing OutboxMessages and InboxMessages DbSets (+ EF config, both
carrying TenantId); a base repository exposing ExecuteInTransactionAsync built for the Aspire Npgsql
execution strategy (callback form, retry-safe); IOutbox (serialize an IIntegrationEvent to an
OutboxMessages row on the same DbContext) and IInbox (record/check a handled message id); a global
exception handler mapping ValidationException -> 400 and the domain exception -> 4xx via a shared
error shape; a cache abstraction; and IIntegrationEventConsumer<TEvent>. Provide
AddSharedPersistence() and AddSharedExceptionHandler() registration extensions. No Service Bus and no
tenancy yet — just the DB-side spine.

CONSTRAINT: Follow .claude/rules/backend.md and .claude/rules/messaging.md. Match the outbox/inbox
contract in the add-endpoint skill exactly (deterministic event Id, at-least-once, same transaction
as the write).

RESTRICTION: Shared holds MECHANISM only — no service's domain, business, ViewModels, or
ServiceModels. Do NOT talk to Service Bus here yet. Do NOT open user-initiated transactions
(ExecuteInTransactionAsync must accept the whole operation as a callback). No hardcoded config.

USAGE: Use plan mode. Delegate the final review to the code-reviewer subagent.

BEHAVIOR: Plan the types and the transaction/outbox contract and show me the interfaces before
writing. Wait for approval. Implement with unit tests for IOutbox (writes a row) and the base repo
(rolls back on a mid-operation throw), run `dotnet test`, run the code-reviewer, and summarize.
```

## Prompt 2 — The tenancy mechanism (PlannerPro.Shared) ← highest-risk step

```
SCOPE: Add the multi-tenancy mechanism to PlannerPro.Shared, once, so every service inherits it:
(1) ITenantScoped { Guid TenantId { get; set; } };
(2) ITenantContext exposing TenantId, Slug, Role, Plan, Status, IsResolved and BypassFilters, as a
    SCOPED service, plus two non-request implementations: SystemTenantContext (bypass, for
    migrate/seed/platform-admin) and DesignTimeTenantContext (bypass, for dotnet ef);
(3) automatic global query filters — in the base DbContext's OnModelCreating, reflect over every
    entity type implementing ITenantScoped and apply HasQueryFilter(e => e.TenantId ==
    _tenant.TenantId || _tenant.BypassFilters). The filter must close over the INJECTED ITenantContext
    instance so it re-evaluates per request; confirm the model-cache implications and handle them;
(4) TenantSaveChangesInterceptor — stamp TenantId on Added ITenantScoped entities from the context,
    and throw CrossTenantWriteException when a Modified or Deleted entity's TenantId differs from the
    current tenant;
(5) TenantContextMiddleware that populates ITenantContext from the gateway's trusted projected
    headers, and an AddSharedTenancy() registration extension.

CONSTRAINT: Follow .claude/rules/tenancy.md. TenantId is a Guid, always. Outbox and inbox rows carry
TenantId too. This is defense in depth — assume each individual layer will one day be bypassed.

RESTRICTION: Do NOT allow a tenant id to be read from a request body, query string, or route value
here — this layer trusts only the projected header (HTTP) or, later, the event envelope (bus). Do NOT
apply a filter to ASP.NET Identity tables; users are global by design. Do NOT expose a public way to
mutate ITenantContext mid-request. Do NOT use IgnoreQueryFilters anywhere except an explicit bypass
context.

USAGE: Use plan mode. Delegate review to code-reviewer AND tenant-isolation-auditor.

BEHAVIOR: Plan the types and — critically — write out the exact filter-evaluation semantics you
expect (when the filter is compiled, when it is re-evaluated, what happens on a pooled context)
BEFORE writing code, and tell me how you'll prove it. Wait for approval. Then implement with tests
that: a query in tenant A cannot see tenant B's rows; an update of tenant B's entity while scoped to
A throws CrossTenantWriteException; a new entity is stamped automatically; and a reflection test
asserts EVERY ITenantScoped type has a filter. Run dotnet test, run both subagents, and summarize.
```

## Prompt 3 — Service Bus + the relay, tenant-aware (PlannerPro.Shared)

```
SCOPE: Add the transport half of messaging to PlannerPro.Shared: an OutboxDispatcher BackgroundService
that polls unprocessed rows oldest-first and sends each as a ServiceBusMessage (MessageId = row Id,
Subject = event type name, and the TenantId/CorrelationId/CausationId promoted to application
properties), then stamps ProcessedOnUtc; a ServiceBusProcessorHost that receives messages, resolves
the registered IIntegrationEventConsumer<TEvent>, and invokes it; and a consumer registry.

THE CRITICAL PART: a consumer runs on a background thread with NO HTTP request, so before invoking
it the processor host must open a scope and establish ITenantContext FROM THE EVENT ENVELOPE's
TenantId. Without that, every consumer either sees nothing (filter matches no tenant) or writes
unstamped rows. Make this explicit, make it central, and make it impossible for a consumer author to
forget.

CONSTRAINT: Follow .claude/rules/messaging.md and .claude/rules/tenancy.md. Delivery is at-least-once;
the inbox is how consumers dedupe. Get the ServiceBusClient from the Aspire integration keyed to the
AppHost resource.

RESTRICTION: Nothing but the dispatcher sends to Service Bus. Do NOT let a consumer resolve its own
tenant, and do NOT let a message without a TenantId be dispatched to a tenant-scoped consumer — fail
it loudly to the dead-letter path instead of processing it under an ambiguous scope. No hardcoded
namespace or connection string.

USAGE: Use plan mode. Delegate review to code-reviewer AND tenant-isolation-auditor.

BEHAVIOR: Plan the envelope shape and the scope-establishment sequence, and show me how a consumer
author is prevented from bypassing it. Wait for approval. Implement with tests: the dispatcher sends
and stamps and leaves failed sends for retry; a redelivered message is applied once; a consumer
invoked for tenant B cannot read tenant A's rows; a message with no TenantId is rejected rather than
silently processed. Run dotnet test, run both subagents, summarize.
```

## Prompt 4 — The gateway: the only door, and the only place tenancy is resolved

```
SCOPE: Build PlannerPro.Gateway (YARP) as the single public entry point and the single tenancy
resolution point. It must: validate the caller's credential; for routes matching /api/t/{slug}/…,
resolve slug -> tenant (memory-cached, short TTL) and then the caller's TenantMembership; STRIP any
client-supplied tenant/actor/correlation headers; project its own trusted set inward (tenant id,
slug, role, tenant status, actor id, correlation id) and echo the correlation id on the response;
route by Aspire resource name; and pass anonymous/user-scoped routes (/api/ping, /api/public/*,
/api/auth/*, /api/signup, /api/invitations/*, /api/me/tenants, /api/admin/*) through WITHOUT tenant
resolution.

CONSTRAINT: Follow .claude/rules/gateway.md and .claude/rules/tenancy.md. Clusters name destinations
by Aspire service-discovery name (e.g. http://planning) — that is the one sanctioned place a service
name appears in config. Keep the gateway declarative: routing and edge concerns only.

RESTRICTION: A caller with no active membership gets 404 — NEVER 403, never a distinct message, never
a different shape that reveals the tenant exists. Tenants whose status is Suspended/PastDue/Cancelled
are READ-ONLY: reads pass, writes are refused — do not implement suspension as a login block. Do NOT
put business logic in the gateway. Do NOT let a service re-resolve a slug.

USAGE: Use plan mode. Delegate review to code-reviewer AND tenant-isolation-auditor.

BEHAVIOR: Plan the route table, the header contract (exact names, who mints, who strips), and the
resolution/caching strategy including cache invalidation on membership change. Wait for approval.
Implement with tests covering: header spoofing is stripped; non-member gets 404; suspended tenant can
GET but not POST; unknown slug is indistinguishable from unauthorized. Run dotnet test, run both
subagents, summarize.
```

## Prompt 5 — The template service: Access host + Access.Core (the mixed-scoping store)

```
SCOPE: Build the first service, PlannerPro.Access + PlannerPro.Access.Core, as the template every
other service copies. It owns accessdb and is deliberately the ONE database with mixed scoping:
ASP.NET Identity tables are GLOBAL (one account per email platform-wide, no tenant filter), while
Tenant, TenantSettings, TenantBranding, TenantMembership and Invitation are tenant-scoped. Build the
full facade -> business -> data layer -> repository stack in .Core over the Shared base context, the
thin host with Controllers/ and Program.cs, an Add AccessCore() registration extension, the entity
model and first migration, and credential/token issuance. Field definitions come from the domain
model in the design docs — Tenant carries Slug (unique), Name, Status, PlanId, TrialEndsAt, and the
three reserved Stripe columns which stay NULL and unused.

CONSTRAINT: Follow .claude/rules/backend.md and .claude/rules/tenancy.md. ViewModels in, ServiceModels
out, never an EF entity at the boundary. Registration lives in .Core. Async throughout. Validate at
the edge with FluentValidation.

RESTRICTION: Do NOT apply a tenant filter to Identity tables — that is the deliberate exception, and
comment it so the next reader doesn't "fix" it. Do NOT put IsAdmin or DefaultCapacityPoints on the
user; both are per-tenant and live on TenantMembership; a separate IsPlatformAdmin flag exists for
staff. Do NOT use FindAsync anywhere. Do NOT seed any credential from source — user-secrets in dev,
environment variables in production, blank in appsettings.json by design.

USAGE: Use the add-endpoint and add-tenant-scoped-entity skills. Use plan mode. Delegate review to
code-reviewer and tenant-isolation-auditor.

BEHAVIOR: Plan the entity model, which types are ITenantScoped and which are deliberately not (and
why, per type), and the migration, and show me before generating anything. Wait for approval. Then
implement, run the migration, run dotnet test, run both subagents, and summarize — including an
explicit statement of the tenancy story for each entity.
```

## Prompt 6 — Prove the loop: tenant provisioning end to end

```
SCOPE: Prove the entire tenant-aware event loop with the smallest real slice. In Access, add the
endpoint that provisions a tenant (tenant + settings + branding + owner membership, in one
transaction) and publishes TenantProvisioned through the outbox in that SAME transaction. Add
PlannerPro.Portfolio + .Core as a second service owning portfoliodb, with a TenantProvisionedConsumer
that creates that tenant's default "Internal" client. Add both to the AppHost with their own
databases. Add the gateway route.

This is the moment the whole spine is under test at once: outbox atomicity, dispatcher relay,
processor-host tenant scoping from the envelope, inbox idempotency, query filters, and the
SaveChanges interceptor.

CONSTRAINT: Follow .claude/rules/messaging.md, .claude/rules/tenancy.md and .claude/rules/backend.md.
The event carries TenantId, CorrelationId, CausationId and actor. The consumer writes ONLY
portfoliodb.

RESTRICTION: Do NOT have Portfolio call Access synchronously, and do NOT give it a second connection
string. Do NOT send to Service Bus from business or data code — the outbox and dispatcher only. Do
NOT let the consumer resolve tenancy itself; it must receive scope from the processor host.

USAGE: Use the add-endpoint skill. Use plan mode. Delegate review to code-reviewer,
test-gap-analyzer, and tenant-isolation-auditor.

BEHAVIOR: Plan the event contract, both sides, and the failure modes (dispatcher crash between send
and stamp; duplicate delivery; consumer throws) before writing. Wait for approval. Implement, then
run `aspire run` and prove the loop for real: provision two tenants, show each got its own client,
show a redelivery is a no-op, and show tenant A's client is invisible from tenant B. Run dotnet test
and all three subagents. Report what you verified and what you did not.
```

## Prompt 7 — Planning: the product core

```
SCOPE: Build PlannerPro.Planning + .Core over planningdb — the heart of the product. Entities: Sprint
(per-tenant numbering), SprintGoal (exactly one per project per sprint, created lazily, unique on
(TenantId, SprintId, ProjectId)), PlannerTask (Fibonacci effort points; named PlannerTask to avoid
clashing with System.Threading.Tasks.Task; table "Tasks"), and SprintCapacity (unique on (TenantId,
UserId, SprintId)). Add the board and timeline read endpoints, goal upsert, task CRUD, and capacity
endpoints. Consume ProjectCreated/ProjectArchived from Portfolio into a local ProjectReference read
model, and TenantSettingsChanged from Access to keep this tenant's sprint cadence and overload
threshold current. Generate a tenant's sprint calendar from ITS OWN settings.

CONSTRAINT: Follow .claude/rules/backend.md and .claude/rules/tenancy.md. Effort is NEVER stored — a
goal's points are the SUM of its tasks, a sprint's total is the sum across projects. The overload
threshold and sprint cadence are PER-TENANT settings, not constants. Validate Fibonacci points
server-side (1,2,3,5,8,13,21); the "over 8" warning is UX only.

RESTRICTION: Do NOT read Portfolio's database — consume the event and keep a local copy. Do NOT
hardcode an overload threshold or a sprint anchor date anywhere; they were global constants in the
single-tenant version and that is exactly the thing being fixed. Do NOT use FindAsync. Do NOT let a
denormalized effort total into the schema.

USAGE: Use the add-endpoint and add-tenant-scoped-entity skills. Use plan mode. Delegate review to
code-reviewer, test-gap-analyzer, and tenant-isolation-auditor.

BEHAVIOR: Plan the entity model, the read-model strategy for projects, and the per-tenant calendar
generation before writing. Wait for approval. Implement, migrate, and test — including that two
tenants with different cadences both produce correct calendars, and that a consumer redelivery
doesn't duplicate a ProjectReference. Run dotnet test and all three subagents.
```

## Prompt 8 — The cross-tenant isolation suite ← do this BEFORE fanning out

```
SCOPE: Build the cross-tenant integration test suite that every later service must pass. It
provisions two tenants with overlapping-looking data, then asserts, for EVERY tenant-scoped read and
write currently in the system: a request from tenant B using tenant A's ids returns 404 — not 403,
not 200 with partial data, not a validation error that reveals the row exists — and mutates nothing.
Add the reflection test that enumerates every ITenantScoped implementation and asserts each has a
query filter, so a future entity cannot be added without one. Add a test that fails the build if any
FindAsync call exists on a tenant-scoped entity.

CONSTRAINT: Follow .claude/rules/tenancy.md. Prefer a harness that exercises the real stack (gateway
header projection included) over unit-level mocks — the failure modes here live in the seams.

RESTRICTION: Do NOT weaken an assertion to make a test pass. If a real leak is found, STOP, report
it, and fix the mechanism in Shared rather than patching the individual endpoint — a leak at one
endpoint means the mechanism failed, and the same failure is about to be copied into six more
services.

USAGE: Use plan mode. Delegate review to test-gap-analyzer and tenant-isolation-auditor.

BEHAVIOR: Plan the harness shape and the matrix of cases before writing. Wait for approval.
Implement, run it, and report the results honestly — including anything that fails. This suite is the
gate for the fan-out; tell me plainly whether we should proceed.
```

## Prompt 9 — Fan out: Roadmap, Files, Audit, Notifications

```
SCOPE: With the pattern proven and the isolation suite green, replicate it across four more services,
one at a time: PlannerPro.Roadmap (+.Core, roadmapdb — the coarse program/Gantt view, consumes
ProjectCreated/Archived); PlannerPro.Files (+.Core, filesdb — task attachments and tenant assets over
Azurite, publishing AttachmentUploaded/AttachmentDeleted with byte sizes); PlannerPro.Audit (+.Core,
auditdb — append-only support trail consuming every business event); PlannerPro.Notifications (+.Core,
notificationsdb — consumes events and sends email, no public HTTP surface). Register each in the
AppHost with its own database, and add gateway routes for the ones with a public surface.

CONSTRAINT: Follow the established pattern exactly — thin host, .Core stack, Shared mechanism, outbox
publish, inbox-idempotent consumers, ITenantScoped entities. Blob names are tenant-prefixed:
{tenantId}/{taskId}/{guid}.ext. Audit is append-only and consumes only; it never calls back into a
service and never drives domain behavior.

RESTRICTION: Do NOT invent new plumbing — if a service seems to need mechanism that isn't in Shared,
stop and tell me rather than building a second version of it. Notifications gets NO gateway route.
Audit NEVER writes to another service's store and never publishes. Do NOT store an unprefixed blob
name, even temporarily.

USAGE: Use the add-endpoint, add-tenant-scoped-entity and add-audit-event skills. Use plan mode.
Delegate review to code-reviewer, test-gap-analyzer and tenant-isolation-auditor after EACH service.

BEHAVIOR: Do these ONE SERVICE AT A TIME, and re-run the cross-tenant suite after each — do not batch
them. Plan each, wait for approval, implement, migrate, test, review, summarize, then ask before
starting the next.
```

## Prompt 10 — Billing: quotas replicated, limits enforced locally

```
SCOPE: Build PlannerPro.Billing + .Core over billingdb: Plan (code free|team|business with MaxUsers,
MaxClients, MaxProjects, MaxStorageMb, price, active flag), per-tenant usage counters, and the
lifecycle Trialing -> Active -> PastDue -> Suspended -> Cancelled. Billing consumes the countable
events (InvitationAccepted, ClientCreated, ProjectCreated, AttachmentUploaded/Deleted) to maintain
counters, and publishes TenantQuotaChanged and TenantStatusChanged.

THE ARCHITECTURAL POINT: a service cannot synchronously ask Billing "am I under my project limit?"
without coupling the write path to another service's availability. So each enforcing service keeps a
LOCAL quota snapshot, updated by TenantQuotaChanged, and enforces against that. Billing is the
authority and reconciles. That is eventual consistency with a deliberate, bounded overshoot window —
implement it knowingly, document the window, and return 402 with a machine-readable limit code so the
SPA can show a targeted upgrade prompt.

CONSTRAINT: Follow .claude/rules/billing.md and .claude/rules/messaging.md. Stripe columns exist and
stay NULL — no Checkout, no webhooks, no live billing. Suspended tenants are READ-ONLY, not locked
out; they can still read and export.

RESTRICTION: Do NOT add a synchronous call from any service to Billing on a write path. Do NOT let a
service compute a limit from its own row count alone without the replicated quota. Do NOT implement
suspension as an auth failure. Do NOT wire any real payment provider.

USAGE: Use the add-endpoint skill. Use plan mode. Delegate review to code-reviewer and
tenant-isolation-auditor.

BEHAVIOR: Plan the counter model, the replication contract, and — explicitly — the overshoot window
and how you'd reconcile it, before writing. Wait for approval. Implement and test: a limit is
enforced; a tenant at its limit gets 402 with the right code; a quota change propagates; a
double-delivered count event does not double-count. Run dotnet test and both subagents.
```

## Prompt 11 — Angular shell: tenant routing, interceptor, tenant context

```
SCOPE: Build the Angular 22 shell in src/web: zoneless, signal-first, standalone components, strict
TS. Nest all authenticated routes under /t/:tenant. Add a TenantContext service exposing slug,
tenant, role, plan and limits as signals, populated by a resolver on the /t/:tenant parent route. Add
ONE HTTP interceptor that rewrites /api/x to /api/t/{slug}/x, with an explicit passthrough list
(/api/auth, /api/public, /api/signup, /api/me, /api/admin, /api/ping). Add authGuard, tenantGuard,
and a roleGuard(minRole) factory. Add typed services per backend resource, all hitting the gateway.

CONSTRAINT: Follow .claude/rules/frontend.md. Reads use httpResource; writes patch local signals
optimistically then reload() to reconcile with the server. Model interfaces mirror each service's
ServiceModels exactly. The gateway base URL comes from Aspire-injected config.

RESTRICTION: Done correctly, NO store or typed service needs a tenant-aware URL — the interceptor is
the only place the slug appears. Keep it that way; a slug hardcoded in a store is the thing this
design exists to prevent. No `any`. No zone.js, no NgModules, no NgZone. Do NOT target a service
directly, only the gateway. Do NOT read the role from anywhere but TenantContext.

USAGE: Use the add-component skill. Use plan mode. Delegate review to code-reviewer and
api-contract-checker.

BEHAVIOR: Plan the routing tree, the interceptor's rewrite/passthrough logic, and the resolver's
failure path (unknown slug, non-member -> what does the user see?) before writing. Wait for approval.
Implement, run `ng test` and `ng build`, run both subagents, and summarize.
```

## Prompt 12 — Core UI: board, timeline, roadmap, capacity

```
SCOPE: Build the main screens against the gateway: sprint board (current sprint, project columns,
inline goal/status/task editing, effort totals and overload flag), timeline (rolling grid of sprints
x projects), roadmap (coarse program view), capacity (per-person point budget per sprint), plus
login, and a client filter on board/roadmap/timeline. Read the overload threshold and limits from
server data.

CONSTRAINT: Follow .claude/rules/frontend.md. Signals for state, httpResource for reads, optimistic
write + reconcile for mutations. A native <select> sets its initial value with [selected] on each
<option>, not [value] on the select — under zoneless change detection the latter does not reflect.

RESTRICTION: Do NOT hardcode the overload threshold, the task-size warning, the sprint cadence, or a
color palette — all are per-tenant data from the API. Do NOT call HttpClient from a component. Do NOT
let a component's local state become the source of truth for effort; the server recomputes and wins.

USAGE: Use the add-component skill. Use plan mode. Delegate review to code-reviewer and
api-contract-checker.

BEHAVIOR: Plan the component tree and the store-per-area split before writing. Wait for approval.
Build one screen at a time, running ng test between them. Summarize with a note on anything you
couldn't verify without real data.
```

## Prompt 13 — Onboarding: signup, slug rules, invitations

```
SCOPE: Add self-serve onboarding. POST /api/signup (anonymous): org name, desired slug, email,
password, display name -> transactionally create tenant (Trialing, 14 days, free plan), settings,
branding defaults, account, Owner membership, sprint calendar, and a sample client + project; then
sign the user in. Slug rules: ^[a-z0-9][a-z0-9-]{1,30}[a-z0-9]$ plus a reserved list (api, auth,
admin, app, www, t, signup, login, health, public, assets, static). Expose GET
/api/public/slug-available. Add invitations: create (Admin+, store only a token HASH, 7-day expiry),
anonymous preview by token, and accept (join as an existing user, or sign up and join). Add the
Angular /signup, /invite/:token and /tenants switcher views.

CONSTRAINT: Follow .claude/rules/tenancy.md. Provisioning is one transaction plus outbox events —
partial tenant creation is not an acceptable failure mode. Email delivery may be stubbed behind an
IInvitationNotifier that logs the link in Development; say clearly that production needs a provider.

RESTRICTION: Do NOT store a raw invitation token — hash only. Do NOT let signup or slug-availability
leak whether a slug belongs to an existing tenant beyond the boolean it must return, and rate-limit
it. Do NOT let a tenant end up with zero active Owners — enforce that guardrail on every membership
change. Do NOT wire a real email provider without telling me first.

USAGE: Use the add-endpoint and add-component skills. Use plan mode. Delegate review to code-reviewer
and tenant-isolation-auditor.

BEHAVIOR: Plan the provisioning transaction, the token lifecycle, and every failure path (duplicate
slug, expired token, already-member, last-owner removal) before writing. Wait for approval.
Implement, test each failure path explicitly, run both subagents, and summarize.
```

## Prompt 14 — White-label branding

```
SCOPE: Add per-tenant branding. TenantBranding CRUD (Admin+): product name, logo, favicon, accent and
core surface colors, login tagline, theme mode. Logo/favicon upload through Files to a tenant-assets
container, reusing the existing type/size validation. GET /api/public/tenants/{slug}/branding —
ANONYMOUS, so the login page is branded before auth. In Angular, a Branding service writes CSS custom
properties onto document.documentElement; the existing :root block in styles.scss becomes the default
theme. Product name replaces "PlannerPro" in the topbar, <title>, and the login page.

CONSTRAINT: Follow .claude/rules/frontend.md and .claude/rules/tenancy.md. Validate color contrast on
save so a tenant cannot make its own app unreadable. Apply branding on tenant resolve and on the
login page.

RESTRICTION: Do NOT touch component styles — the whole point is that theming happens through the
custom properties the app already uses. The anonymous endpoint returns ONLY public-safe fields, and
returns generic defaults (not 404) for an unknown slug so it can't be used to enumerate tenants;
rate-limit it. Do NOT serve a tenant asset without verifying its row through the filtered query
first.

USAGE: Use the add-endpoint and add-component skills. Use plan mode. Delegate review to code-reviewer
and tenant-isolation-auditor.

BEHAVIOR: Plan the branding contract, the load order (anonymous on login vs authenticated in-app),
and the contrast rule before writing. Wait for approval. Implement, verify two tenants render
differently with no component CSS changes, run both subagents, summarize.
```

## Prompt 15 — End-to-end run + verification

```
SCOPE: Run the whole system and verify it honestly, end to end. Start with `aspire run`. Provision
two tenants through real signup. In each, create clients and projects, set sprint goals, add tasks,
upload an attachment, invite a member, and hit a plan limit. Then verify: every service healthy;
traces followable from gateway -> service -> bus -> consumer with TenantId tagged; the cross-tenant
suite green; a redelivered message a no-op; a suspended tenant read-only; branding distinct per
tenant; the audit trail able to reconstruct one request cradle-to-grave by correlation id.

CONSTRAINT: Follow .claude/rules/audit.md for the trail check and use the trace-a-request skill.

RESTRICTION: Do NOT fix anything you find during this pass without telling me first — I want the
honest inventory before the repairs. Do NOT report something as verified that you inferred from code
rather than observed running.

USAGE: Use the trace-a-request skill. Delegate to test-gap-analyzer and tenant-isolation-auditor.

BEHAVIOR: Produce a verification report: what you exercised, what passed, what failed, what you could
not verify and why. Rank the failures by risk, with tenant-isolation issues first regardless of how
small they look. Then wait — don't start fixing.
```

## Prompt 16 — Stretch: platform operations

```
SCOPE: Add the platform surface: /api/admin/tenants console gated on IsPlatformAdmin (list with
usage, suspend/reactivate, change plan); per-tenant rate limiting at the gateway; TenantId as a tag
on OpenTelemetry spans and log scopes via ServiceDefaults; an audit log of tenant-level
administrative actions; and tenant data export (JSON) plus hard-delete behind a retention window.

CONSTRAINT: Follow .claude/rules/gateway.md, .claude/rules/audit.md and .claude/rules/tenancy.md. The
platform surface is the ONLY place outside migration and seeding that runs under a bypass context.

RESTRICTION: Do NOT expose the platform routes through the tenant-scoped group. Do NOT let a
platform-admin action skip the audit trail. Do NOT implement hard-delete without the retention
window and a confirmation step. Every bypass-context usage must be narrow, explicit, and commented.

USAGE: Use the add-endpoint and add-audit-event skills. Use plan mode. Delegate review to
code-reviewer and tenant-isolation-auditor.

BEHAVIOR: Plan the surface and, specifically, every place a filter bypass occurs and why. Wait for
approval. Implement, test that a non-platform-admin gets 404 on every platform route, run both
subagents, summarize.
```

---

# Part 2 — Operational templates (reuse anytime)

## Template A — Feature delivery (vertical slice within one service)

```
SCOPE: Add <feature> to the <service> service. <Describe the endpoint(s), the data, and the UI.>

CONSTRAINT: Follow .claude/rules/backend.md, .claude/rules/tenancy.md and .claude/rules/frontend.md.
Stay inside <service> and its own database. ViewModels in, ServiceModels out.

RESTRICTION: Do NOT touch another service's database or add a synchronous call to another service. Do
NOT use FindAsync. Do NOT add a tenant filter bypass. If this feature seems to need data another
service owns, STOP and tell me — that's an event, not a query.

USAGE: Use the add-endpoint skill, then the add-component skill for the UI.

BEHAVIOR: Plan first — name the service, the entities, the tenancy story, and the files you'll touch.
Wait for approval. Implement in small steps, run that service's tests, then run code-reviewer and
tenant-isolation-auditor. Report what's verified and what isn't.
```

## Template B — Cross-service change (event across the bus)

```
SCOPE: <Describe what must happen in service A and what service B must do in response.>

CONSTRAINT: Follow .claude/rules/messaging.md and .claude/rules/tenancy.md. The event is an immutable
past-tense record in Contracts carrying TenantId, CorrelationId, CausationId and actor. A publishes
through its outbox in the same transaction as its write; B consumes idempotently via its inbox and
writes only its own database.

RESTRICTION: Do NOT add a synchronous call between the services. Do NOT let B reach into A's data. Do
NOT change an existing event's shape without listing every consumer and updating them in the same
change — that's a contract change. Do NOT publish an event without a TenantId.

USAGE: Use the add-endpoint skill on both sides.

BEHAVIOR: Plan the event contract FIRST and show it to me before any code — including what happens on
duplicate delivery, on consumer failure, and on a dispatcher crash between send and stamp. Wait for
approval. Implement both sides, run both services' tests, then run code-reviewer,
api-contract-checker and tenant-isolation-auditor.
```

## Template C — New tenant-scoped entity + migration

```
SCOPE: Add <entity> to the <service> service. <Fields, relationships, uniqueness.>

CONSTRAINT: Follow .claude/rules/tenancy.md and .claude/rules/backend.md. The entity implements
ITenantScoped. EVERY uniqueness constraint is tenant-scoped — (TenantId, X), never (X) alone. The
DbContext is in .Core; the host is the startup project for dotnet ef.

RESTRICTION: Do NOT create a globally unique index on a tenant-scoped entity — that leaks the
existence of other tenants' data through constraint violations and is the most commonly missed rule
here. Do NOT hand-edit the generated migration beyond reviewing it. Do NOT apply it before I've seen
it.

USAGE: Use the add-tenant-scoped-entity skill.

BEHAVIOR: Show me the entity, the indexes, and the intended migration name BEFORE generating.
Wait for approval. Generate, show me the migration diff, wait again, then apply. Run the reflection
test that asserts every ITenantScoped type has a filter, run the cross-tenant suite, and run
tenant-isolation-auditor.
```

## Template D — Refactor / cross-cutting change

```
SCOPE: <Describe the refactor and every service it touches.>

CONSTRAINT: Behavior must not change. Follow the existing patterns rather than introducing new ones.
If the change belongs in Shared, it must be MECHANISM — no service's domain.

RESTRICTION: Do NOT change public API shapes, event contracts, or database schemas as part of a
refactor — if one is needed, stop and we'll do it as its own change. Do NOT refactor more than one
service per pass.

USAGE: Delegate the review to code-reviewer.

BEHAVIOR: Plan the full blast radius before touching anything, listing every file and every service.
Wait for approval. Work one service at a time, running its tests after each, and re-run the
cross-tenant suite at the end. Report anything that changed behavior — even slightly.
```

## Template E — Tenant isolation review

```
SCOPE: Audit <the change / this service / the whole system> for cross-tenant leakage.

CONSTRAINT: Follow .claude/rules/tenancy.md. Check all five layers, not just the one that looks
relevant: query filters, the SaveChanges interceptor, gateway resolution and header stripping, role
filters, and the consumer's envelope-derived scope.

RESTRICTION: This is READ-ONLY — report, do not fix. Do NOT downgrade a finding because it "would be
hard to exploit." Do NOT skip the bus path; consumers are where scope is most often lost.

USAGE: Delegate to tenant-isolation-auditor. Also run test-gap-analyzer for missing isolation tests.

BEHAVIOR: Report findings ranked by severity, each with the file, the specific path a caller from
another tenant would take, and the concrete fix. Explicitly list what you checked and found clean —
a bare "no issues" is indistinguishable from not having looked. Then wait.
```

## Template F — Debug / harden

```
SCOPE: <Describe the symptom precisely — what you did, what happened, what you expected.>

CONSTRAINT: Diagnose before changing anything. Use the Aspire dashboard's logs and traces, and the
audit trail for "what happened" questions. Remember that in this system a symptom of "no data" is
very often a tenancy scope problem, not a query bug.

RESTRICTION: Do NOT change code until you can state the root cause. Do NOT "fix" it by adding
IgnoreQueryFilters or by widening a filter — if that appears to fix it, you've found a scoping bug
and hidden it. Do NOT fix more than the one thing.

USAGE: Use the trace-a-request skill for request-history questions.

BEHAVIOR: Investigate, then tell me the root cause and the smallest fix before implementing. Wait for
approval. Implement, add the regression test that would have caught it, and run the relevant
subagents.
```

## Pro tips

- **Read the plan, not the code.** The highest-leverage thirty seconds you spend is on the plan Claude shows before it writes. Boundary mistakes and missing tenancy stories are obvious there and expensive later.
- **`/clear` between prompts.** Rules and skills reload themselves; stale context is what makes long sessions drift.
- **The tenancy question, every time.** "Where does `TenantId` come from on this path?" has exactly two right answers: the gateway's projected header, or the event envelope. Any third answer is a bug.
- **A leak found in one service is a mechanism failure.** Fix it in `PlannerPro.Shared` and re-run the suite everywhere. Patching the one endpoint means the other seven still have it.
- **When you reuse a Part 2 template three times, promote it to a skill.** That's the signal it's a procedure, not a prompt.
