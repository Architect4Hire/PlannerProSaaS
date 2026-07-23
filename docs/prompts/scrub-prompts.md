# PlannerPro — SCRUB Prompts

Prompts for driving Claude Code on PlannerPro (Aspire + ASP.NET Core microservices + Angular, multi-tenant SaaS), all wired to the skills, rules, and subagents in your `.claude/` folder.

Two parts:

- **Part 1 — Scaffolding:** a one-time sequence, run in order, to stand the whole thing up — services, app, public front door, and deployment. Because this is a *multi-tenant microservice* app, the sequence stands up the shared spine first, then the **tenancy mechanism**, then proves **one** service and the **whole tenant-aware event loop** end to end — and only then fans out.
- **Part 3 — Proposed work:** a candidate AI capability (the Sprint Advisor), **designed but not scheduled**. It runs only after its own ADRs are written and approved. Versioned here with everything else, but it is a proposal, not a plan.
- **Part 2 — Operational templates:** reusable prompts for the recurring, high-stakes moments the agent won't self-guard (features that cross the bus, migrations, refactors, isolation review, debugging). Fill in the blanks and go.

Once the system is scaffolded, most day-to-day work needs no bespoke prompt — your rules, skills, subagents, and hooks carry the structure. Reach for Part 2 only when a task is non-trivial or risky. When you reuse one two or three times, promote it to a skill so you stop needing the prompt at all.

## The public front door

From Prompt 11 onward the system has a public shape, on **one hostname**, in three non-overlapping
namespaces:

| Path | Serves | Owned by |
| --- | --- | --- |
| `/`, `/privacy`, `/terms` | static landing content | static files |
| `/app`, `/app/login`, `/app/tenants`, `/app/t/{slug}/…` | the Angular SPA | SPA shell |
| `/api/**` | everything else | gateway |

Route precedence is ordered, and `/api` wins: **`/api/**` → `/app/**` → static → 404.** Get the first
two backwards and it looks like the gateway is down.

Same origin throughout, which is what preserves ADR-0007's `SameSite=Strict` cookie plus antiforgery
with no CORS. Moving the app to another subdomain or domain would break cross-site navigation and
force CORS — it needs an ADR superseding ADR-0007 and ADR-0021, not a config change.

**This is a client tool, not a self-serve SaaS.** Tenants are provisioned by a platform admin as part
of an engagement (Prompt 14). There is no public signup, which is why the landing page's CTA is
"Log in" and why several prompts explicitly forbid building a signup flow.

## Contents

**[Part 1 — Scaffolding](#part-1--scaffolding-run-once-in-order)** — run once, in order, 0 → 22.

| | | | |
| --- | --- | --- | --- |
| **0** Solution skeleton | **6** Prove the loop | **12** Angular shell under `/app` | **18** Serve the three namespaces |
| **1** Shared spine | **7** Planning (product core) | **13** Core UI | **19** End-to-end verification |
| **2** Tenancy mechanism ⚠ | **8** Isolation suite 🚧 | **14** Client provisioning | **20** Deploy the front door |
| **3** Service Bus + relay ⚠ | **9** Fan out (4 services) | **15** White-label branding | **21** Legal & analytics minimum |
| **4** Gateway | **10** Billing + quotas | **16** Landing page copy | **22** Platform operations |
| **5** Access service | **11** Front-door ADR 🚧 | **17** Static landing page | |

⚠ contains a *prove-it-first* step · 🚧 a gate — do not pass until green

**[Part 2 — Operational templates](#part-2--operational-templates-reuse-anytime)** — reach for these anytime.

**A** Feature slice · **B** Cross-service event · **C** New tenant-scoped entity · **D** Refactor ·
**E** Isolation review · **F** Debug/harden · **G** Update the landing page · **H** Client-branded entry ·
**I** Rename the product · **J** Investigate a suspected leak

**[Part 3 — Proposed: Sprint Advisor](#part-3--proposed-sprint-advisor)** — *designed, not scheduled.*
A0–A8 plus templates **K** and **L**.

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
- **Two gates govern the order.** The tenancy mechanism (Prompts 2 and 3) and the isolation suite (Prompt 8) come *before* the fan-out. And the front-door topology ADR (Prompt 11) comes *before* the Angular shell (Prompt 12), because it fixes the route shape the shell implements — deciding it afterwards means rewriting the router tree, both guards, the resolver, and every routerLink.
- **The golden rule of the sequence:** the tenancy mechanism (Prompts 2 and 3) and the isolation test suite (Prompt 8) come *before* the fan-out. A tenant leak replicated across eight services is eight times the work to fix, and you won't find it by reading code.

---

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

### Step A — prove the assumption before building on it

*The whole isolation model rests on one EF behaviour nobody has verified. Twenty minutes here
against a week of rework. **Re-run this on every EF Core upgrade** — nothing will tell you when
it stops holding.*

```
SCOPE: Prove — with a test, not an argument — that an EF Core global query filter closing over an
INJECTED, SCOPED ITenantContext re-evaluates per request rather than capturing the first tenant the
compiled model ever saw. Build the smallest possible harness: one entity implementing ITenantScoped,
a base DbContext applying the filter by reflection, a scoped ITenantContext, and a test that resolves
the context twice for two different tenants and asserts each sees only its own rows.

CONSTRAINT: Test the configuration we will actually run — including DbContext pooling if we intend to
use it, and the Aspire Npgsql registration. A test that passes without pooling and fails with it has
told us nothing useful.

RESTRICTION: Do NOT proceed to build anything on this assumption until the test passes. Do NOT make
the test pass by disabling pooling or the model cache without telling me that's what you did — that
is a finding, not a fix. Do NOT assume the behaviour from documentation or from memory of another EF
version.

USAGE: Use plan mode.

BEHAVIOR: First write out, in prose, exactly what you expect: when the filter expression is compiled,
when it is evaluated, and what the model cache holds. Show me that before writing code — if the
prediction is wrong, that's the most valuable output of this prompt. Then implement, run, and report
the actual result. If it fails, STOP and tell me; do not work around it.
```

### Step B — build the mechanism

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

### Then prove it, before any consumer depends on it

*Two failure modes here are silent: an unset context makes every consumer a no-op, and a
defaulted bypass makes every consumer cross-tenant. Neither throws.*

```
SCOPE: Prove that a Service Bus consumer operates under the tenant carried on the event envelope, and
that the two silent failure modes are impossible. Build a harness with two tenants, one ITenantScoped
entity, and a consumer that writes to it. Assert: (1) a message for tenant A causes a write visible
only to tenant A; (2) a consumer invoked with no established scope CANNOT silently write unstamped or
cross-tenant rows; (3) a message arriving with no TenantId is dead-lettered, not processed.

CONSTRAINT: Scope establishment must live in the processor host, be unavoidable, and require nothing
from the consumer author. Follow .claude/rules/messaging.md and .claude/rules/tenancy.md.

RESTRICTION: Do NOT let a consumer resolve its own tenancy from the payload body. Do NOT default
BypassFilters to true for background work. Do NOT make the "no TenantId" case a warning that then
processes anyway — dead-letter it.

USAGE: Use plan mode. Delegate review to tenant-isolation-auditor.

BEHAVIOR: Show me the scope-establishment sequence and explain how a consumer author is structurally
prevented from bypassing it. Wait for approval. Implement, test all three assertions, and report. Say
explicitly whether failure mode A (silent no-op) would be detectable in production logs — if not,
propose what would make it detectable.
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

## Prompt 11 — Front-door topology: the `/app` namespace ADR

```
SCOPE: Do not write implementation code. Write the ADR that records how the public front door is
served, following docs/adr/README.md format. Use the next free number — check docs/adr/ first.

The ADR must record TWO things:

  (1) The landing page is STATIC content served at / on the same origin as the app and the API,
      because ADR-0007's single-origin cookie + antiforgery model is what is being protected.
      Evaluate and choose between:
        (a) the gateway serves static files with fallback to its existing routes
        (b) a CDN / front door in front of both, routing static to storage and the rest to the gateway
        (c) the Angular app serves the landing page as its root route

  (2) The app is namespaced under /app — /app/login, /app/tenants, /app/t/{slug}/… — rather than
      sitting at the root. This is an AMENDMENT to ADR-0007, which documents app routes as
      /t/{slug}/…. Record it as an amendment, state that ADR-0007's reserved-slug list already
      contains `app` so there is no collision risk, and state the ordered route precedence:
      /api/** first, then /app/**, then static, then 404.

CONSTRAINT: The ADR must engage honestly with the fact that ADR-0006 says the gateway is the only
public door and stays declarative with no business logic. Static file serving is an edge concern, not
business logic — but it does couple marketing deploys to gateway deploys, and that cost must be
stated, not waved through. For (2), state the real trade: a longer URL in exchange for a single
prefix rule instead of an enumerated exception list.

RESTRICTION: Do NOT choose option (c) without stating plainly that it loads the entire SPA bundle to
render a brochure and makes the client's first impression a spinner. Do NOT propose a separate
subdomain or domain for the app — that breaks SameSite behaviour on cross-site navigation and forces
CORS, unwinding three ADRs. If you believe a separate origin is right anyway, say so explicitly and
write it as superseding ADR-0007 and ADR-0021, not as a footnote. Do NOT record the /app namespace as
an implementation detail — it changes documented route shape, so it is an amendment.

USAGE: Read ADR-0006, ADR-0007, ADR-0019 and ADR-0021 first.

BEHAVIOR: Draft the ADR and STOP. I want to approve the topology before any page exists, because it
determines the build target, the deploy pipeline, and whether the marketing site can ship
independently of the gateway.
```

## Prompt 12 — Angular shell under `/app`: routing, interceptor, tenant resolver

```
SCOPE: Build the Angular 22 shell in src/web: zoneless, signal-first, standalone components, strict
TS. Everything the SPA owns lives under /app (ADR from Prompt 11):

    /app                     the shell — boots, then RESOLVES where the user belongs
    /app/login               login
    /app/tenants             tenant switcher
    /app/t/:tenant/...       the app proper

The /app resolver handles four arrival cases:
    - not authenticated         → /app/login (remembering the intended destination)
    - authenticated, 0 tenants  → a "request access" view (there is no self-serve signup)
    - authenticated, 1 tenant   → /app/t/{slug}/board
    - authenticated, 2+ tenants → /app/tenants

Add a TenantContext service exposing slug, tenant, role, plan and limits as signals, populated by a
resolver on the /app/t/:tenant parent route. Add ONE HTTP interceptor that rewrites /api/x to
/api/t/{slug}/x, with an explicit passthrough list (/api/auth, /api/public, /api/me, /api/admin,
/api/ping). Add authGuard, tenantGuard, and a roleGuard(minRole) factory. Add typed services per
backend resource, all hitting the gateway.

CONSTRAINT: Follow .claude/rules/frontend.md and ADR-0021. Reads use httpResource; writes patch local
signals optimistically then reload() to reconcile. Model interfaces mirror each service's
ServiceModels exactly. The gateway base URL comes from Aspire-injected config. Resolution happens
CLIENT-SIDE after the shell boots, using GET /api/me/tenants — the server cannot know the destination
until the cookie is validated and the tenant list is known.

RESTRICTION: Done correctly, NO store or typed service needs a tenant-aware URL — the interceptor is
the only place the slug appears. Keep it that way. Do NOT implement /app as a server-side redirect,
and NEVER a 301 — browsers cache permanent redirects, so a user who logs out, joins a second client,
or is offboarded gets pinned to a tenant they cannot reach, and the remedy is asking a client to clear
their cache. Do NOT build a self-serve signup route. Do NOT make an unknown slug and an unauthorized
slug look different — the client cannot reveal what the gateway deliberately hides. No `any`, no
zone.js, no NgModules, no NgZone. Do NOT target a service directly, only the gateway. Do NOT read the
role from anywhere but TenantContext. Do NOT store anything in localStorage.

USAGE: Use the add-component skill. Use plan mode. Delegate review to code-reviewer,
api-contract-checker and tenant-isolation-auditor.

BEHAVIOR: Plan the routing tree, the interceptor's rewrite/passthrough logic, and all four resolver
cases before writing. Wait for approval. Implement, run `ng test` and `ng build`, then test each
arrival case by hand — plus a deep link to /app/t/{slug}/board while logged out, which must land on
login and then return the user to where they were going.
```

## Prompt 13 — Core UI: board, timeline, roadmap, capacity

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

## Prompt 14 — Client provisioning and invitations

*Tenants are provisioned as part of a consulting engagement, not by self-serve signup. That deletes a
phase of work — build the admin path, not the public one.*

```
SCOPE: Build tenant provisioning as a PLATFORM-ADMIN operation plus client-side team invitations.

  1. POST /api/admin/tenants (IsPlatformAdmin only): org name, slug, owner email, display name →
     transactionally create tenant (Active, chosen plan), settings, branding defaults, the owner's
     ApplicationUser if new, an Owner membership, the sprint calendar, and a starter client. Publish
     TenantProvisioned through the outbox in the same transaction.
  2. Slug rules: ^[a-z0-9][a-z0-9-]{1,30}[a-z0-9]$ plus a reserved list (api, auth, admin, app, www,
     t, signup, login, health, public, assets, static). Validate on the admin path.
  3. Invitations, so a client owner can add their own team: POST /api/t/{tenant}/invitations
     (Admin+, store only a token HASH, 7-day expiry), GET /api/invitations/{token} (anonymous
     preview), POST /api/invitations/{token}/accept (join as an existing user, or set a password and
     join).
  4. Angular: an admin provisioning view, /app/invite/:token, and the /app/tenants switcher.

CONSTRAINT: Provisioning is one transaction plus outbox events — a half-created tenant is not an
acceptable failure mode. Email delivery may be stubbed behind an IInvitationNotifier that logs the
link in Development; say clearly that production needs a provider.

RESTRICTION: Do NOT build a public /api/signup endpoint or a slug-availability check — there is no
self-serve signup in this model, and building one contradicts it. Do NOT store a raw invitation
token; hash only. Do NOT let a tenant end up with zero active Owners — enforce that on every
membership change. Do NOT let the anonymous invitation preview reveal the tenant's name to an invalid
token. Do NOT wire a real email provider without telling me first.

USAGE: Use the add-endpoint and add-component skills. Use plan mode. Delegate review to code-reviewer
and tenant-isolation-auditor.

BEHAVIOR: Plan the provisioning transaction, the token lifecycle, and every failure path (duplicate
slug, reserved slug, expired token, already-member, last-owner removal) before writing. Wait for
approval. Implement, test each failure path explicitly, run both subagents, and summarize. Tell me
what a newly provisioned client's owner actually sees on first login — if the answer is "an empty
board," say so, because that is the onboarding gap product-completeness.md flags.
```

## Prompt 15 — White-label branding

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

## Prompt 16 — Landing page copy — write it before any markup

*The most commonly skipped step and the one that determines whether the page is any good. A landing page is a writing problem with a rendering step at the end.*

```
SCOPE: Write the complete copy for the <PRODUCT> landing page as a markdown document —
docs/design/landing-page-copy.md — with no HTML, no CSS, no code. Every headline, every paragraph,
every button label, every alt text, in final form.

The page serves a consulting client who has been told about the tool and is arriving to use it, plus
their colleagues who have not. Sections to draft:
  - Hero: what it is and the one thing it does that a spreadsheet doesn't, in under 20 words
  - The core insight: an overloaded sprint should be visible BEFORE it is lived through — the
    Fibonacci points, the per-tenant overload threshold, the capacity picture across parallel clients
  - Who it's for: agencies and consultancies running several client engagements at once
  - What it looks like: 3–4 screenshot captions (board, timeline, roadmap, capacity)
  - What it does not do yet: an honest short list
  - Log in (→ /app) and request access
  - Footer: built and operated by Architect4Hire, link to the repo

CONSTRAINT: Match the Real Talk voice used across every deliverable in this repo — first person,
direct, no hype, honest about tradeoffs. Read docs/design/product-completeness.md and AGENTS.md first
so the claims are true. The tenant hierarchy is Tenant → Client → Project; effort is Fibonacci points;
the overload threshold is per-tenant, not a constant.

RESTRICTION: Do NOT write "10x", "supercharge", "AI-powered", "revolutionary", "seamless", or
"effortless". Do NOT claim capabilities marked 🔴 in product-completeness.md — no self-serve trials,
no integrations, no mobile app, no reporting. Do NOT invent a customer testimonial, a logo wall, a
statistic, or a case study. Do NOT write a pricing section — pricing happens in a conversation.

USAGE: Use plan mode.

BEHAVIOR: Draft the copy, then tell me which claims you could NOT substantiate from the repo and left
out. That list is more useful to me than the copy. Wait for approval before anything is rendered.
```

## Prompt 17 — Build the static landing page

```
SCOPE: Build the landing page as a static site from the approved copy. Single page, semantic HTML,
no framework, no build step unless one is genuinely warranted. Output to the location the topology ADR
(L0) specified. Include: responsive layout, the brand palette, real screenshots or honest
placeholders, an OG image and meta tags, a favicon, and a "Log in" button pointing at /app whose
destination is configurable rather than hardcoded.

CONSTRAINT: Follow the brand direction in the repo. Accessibility is not optional: semantic
landmarks, real heading hierarchy, alt text on every image, visible focus states, and WCAG AA contrast
— the app already validates contrast for tenant branding (ADR-0019), so the marketing page should not
be worse than the product. Target a fast first paint on a mid-range laptop over hotel wifi, because
that is where a client will actually open it.

RESTRICTION: Do NOT add a JavaScript framework, a CSS framework, or a build pipeline for a single
static page — justify any dependency before adding it. Do NOT put a signup or login FORM on this page:
credentials are entered on the app's own route under /app, and a form here would either need CORS or
would fail to set the SameSite=Strict cookie. The button is a LINK to /app. Do NOT add tracking
scripts, chat widgets, cookie banners, or third-party fonts. Do NOT invent copy — if something is
missing, come back to L1.

USAGE: Use the add-component skill only if the ADR chose an Angular-hosted page; otherwise plain files.

BEHAVIOR: Plan the section structure and asset list before writing. Wait for approval. Build it, then
report the page weight, the request count, and what you used as placeholder assets.
```

## Prompt 18 — Serve the three namespaces on one origin

```
SCOPE: Implement the topology decided in L0 so that on one hostname the ordered route precedence is:

    1. /api/**   → the gateway's existing route table, untouched
    2. /app/**   → the SPA shell (index.html), INCLUDING deep links like /app/t/acme/board
    3. /  and known static paths → the landing page and its assets
    4. anything else → 404

Wire it into the Aspire AppHost so `aspire run` serves the whole thing locally.

CONSTRAINT: Follow .claude/rules/gateway.md and .claude/rules/aspire.md. The gateway stays
declarative — routing and edge concerns only. The SPA fallback must be scoped to /app/** ONLY. The
existing API route table wins over everything.

RESTRICTION: Do NOT introduce CORS. Do NOT change the cookie's SameSite, Secure, HttpOnly, or Path
settings to make anything work — the cookie stays at Path=/ ; scoping it to /app looks tidy and breaks
antiforgery in ways that are tedious to diagnose. If something appears to require a cookie change,
stop and tell me, because it means the topology is wrong. Do NOT let the static handler or the SPA
fallback swallow /api. Do NOT add business logic to the gateway.

USAGE: Use the add-aspire-resource skill if a new resource is needed. Delegate review to
code-reviewer.

BEHAVIOR: Plan the route precedence explicitly — show me the ordered match list before implementing,
because an over-broad fallback that shadows /api is the failure mode here. Wait for approval.
Implement, run `aspire run`, and verify all six cases: /, /app, /app/login, /app/t/{slug}/board typed
directly as a deep link, /api/ping, and an unknown path.
```

## Prompt 19 — End-to-end run + verification (including the front door)

```
SCOPE: Run the whole system and verify it honestly, end to end. Start with `aspire run`. Provision
two tenants through real signup. In each, create clients and projects, set sprint goals, add tasks,
upload an attachment, invite a member, and hit a plan limit. Then verify: every service healthy;
traces followable from gateway -> service -> bus -> consumer with TenantId tagged; the cross-tenant
suite green; the front door correct (/ serves the landing page, /app resolves all four arrival cases,
/app/t/{slug}/board works as a typed deep link, /api/ping reaches the gateway, unknown paths 404); a redelivered message a no-op; a suspended tenant read-only; branding distinct per
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

## Prompt 20 — Deploy the front door

*This is the project's first deployment. Treat it as such — it's a forcing function for decisions that have been deferred.*

```
SCOPE: Deploy the front door to plannerpro.architect4hire.com (or the final hostname). Set up DNS,
TLS, and a GitHub Actions workflow that deploys on merge to the default branch. Document the whole
thing in docs/developer/deploying.md — including how to roll back.

CONSTRAINT: Follow the existing CI conventions in .github/workflows/. Secrets come from GitHub
environment secrets, never from source. If the topology ADR coupled the landing page to the gateway,
say plainly in the doc that a copy change now requires a gateway deploy.

RESTRICTION: Do NOT deploy the API, the services, or the database as part of this — the landing page
and the app's public shell only, unless I say otherwise. Do NOT put any credential, connection string,
or key in a workflow file. Do NOT skip the rollback procedure; a marketing page with no way back is
the one that will need one.

USAGE: Use plan mode.

BEHAVIOR: Plan the hosting target, DNS records, certificate approach, and workflow before doing
anything. Wait for approval. Then implement, deploy, and report the live URL, the TLS grade, and
what is NOT yet deployed behind it — I want the gap stated explicitly rather than implied.
```

## Prompt 21 — The legal and analytics minimum

```
SCOPE: Add only what is actually required: a privacy notice covering what the app stores and what (if
anything) the landing page collects, a terms page appropriate to a client tool rather than a public
SaaS, and — only if I ask for it — privacy-respecting analytics with no cookie banner.

CONSTRAINT: The tone matches the rest of the site: plain language, short, readable. The privacy notice
must reflect what the system ACTUALLY does — read the audit trail design (ADR-0016) and note that a
durable record of user actions exists, because that is a real disclosure.

RESTRICTION: Do NOT add Google Analytics, a cookie consent banner, or any tracker that would require
one. Do NOT generate legal text and present it as sufficient — flag clearly that a lawyer should
review anything that will govern a client relationship. Do NOT claim compliance with any framework
(GDPR, SOC 2, HIPAA) that has not actually been assessed.

USAGE: Use plan mode.

BEHAVIOR: Draft, then list the specific claims you are unsure about and recommend which need legal
review. Wait before publishing anything.
```

---


## Prompt 22 — Stretch: platform operations

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

---


---


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

*Extra guardrails when the entity is new to the model:*

- Which **service owns it** — if two seem to, it is one owner plus an integration event, never a shared table.
- Is it genuinely tenant-scoped? The deliberate exceptions here are Identity tables and `Plan`. A new global entity is a defended decision, not a default.
- Does it duplicate another service's data? A small event-fed read model is correct; a second copy of an authoritative table is not.
- Confirm the reflection test **actually enumerates** the new type — don't assume it does.

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

*Grep vectors to run every time:* `FindAsync`/`Find(`, `IgnoreQueryFilters`, unique indexes
without `TenantId`, direct `db.Users` queries, cache keys with no tenant segment, unprefixed blob
names, and raw SQL against tenant-scoped tables.

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

## Template G — Update the landing page when the product changes

```
SCOPE: The product now does <X>. Update the landing page to reflect it.

CONSTRAINT: Update docs/design/landing-page-copy.md FIRST, then the page. The copy doc is the source
of truth; a page edited directly will drift from it within a month. Match the Real Talk voice.

RESTRICTION: Do NOT claim anything still marked 🔴 in docs/design/product-completeness.md. Do NOT
quietly remove an item from the "what it doesn't do yet" list unless it genuinely now does it — that
list is the page's credibility.

BEHAVIOR: Show me the copy diff before touching the page.
```

## Template H — Add a client-branded entry point

```
SCOPE: Give <client> a branded arrival at /app/t/<slug> that shows their branding before
authentication.

CONSTRAINT: Use the existing anonymous branding endpoint (ADR-0019). Branding applies as CSS custom
properties on document.documentElement; the :root block is the default theme.

RESTRICTION: Do NOT edit component styles to theme anything. Do NOT return a 404 for an unknown slug —
generic defaults only, so the endpoint cannot be used to enumerate clients. Do NOT expose anything
beyond public-safe branding fields. Do NOT brand the marketing page at / — it is yours, not theirs.

USAGE: Delegate review to tenant-isolation-auditor.

BEHAVIOR: Verify a second client's slug renders differently with no component CSS changes.
```

## Template I — Rename the product

```
SCOPE: Rename the product from <OLD> to <NEW> across the repository.

CONSTRAINT: Cover all of: .NET namespaces and project names, the solution file, Aspire resource names,
database names, the Angular app, docs (including every ADR), AGENTS.md, CLAUDE.md, .claude/ and
.github/, the landing page copy, meta tags, OG image, and the hostname.

RESTRICTION: Do NOT rename database resources without noting that existing local volumes will be
orphaned and how to migrate or discard them. Do NOT rename in a single commit mixed with other
changes. Do NOT update generated files by hand — change .claude/ and run .github/sync-copilot.py.

BEHAVIOR: Produce the complete inventory of affected files FIRST, grouped by category, and show me the
count. Wait for approval. Then rename in one focused commit, run the full build, tests, and
`sync-copilot.py --check`, and report anything that needed a manual decision.
```

---

## Template J — Investigate a suspected leak

*If you're running this against a real report, treat it as an incident.*

```
SCOPE: A user reports seeing data that does not belong to their tenant: <what they saw, where, when,
which tenant they were in>.

CONSTRAINT: Diagnose before changing anything. Establish the path first: was it an HTTP request or
something rendered from a consumer-fed read model? That single distinction eliminates half the
possible causes.

RESTRICTION: Do NOT change code until you can state the root cause and name which of the five layers
failed. Do NOT "fix" it by widening a filter, adding IgnoreQueryFilters, or special-casing the
endpoint. Do NOT close the investigation at the first plausible cause — check whether the same
mechanism failure exists in the other seven services.

USAGE: Use the trace-a-request skill to reconstruct what actually happened from the audit trail.
Delegate to tenant-isolation-auditor.

BEHAVIOR: Report the root cause, which layer failed, the blast radius (which other services share the
same mechanism), and the smallest correct fix — in Shared if the mechanism failed. Add the regression
test that would have caught it. Tell me what data was exposed and to whom, as precisely as the trail
allows; that question will be asked and a vague answer is worse than none.
```

---

---

# Part 3 — Proposed: Sprint Advisor

> **Status: proposed, not scheduled.** A candidate ninth bounded context producing a pre-sprint risk
> brief grounded in each tenant's own history — **Semantic Kernel** for orchestration, **OpenAI** for
> inference, **Azure AI Foundry** for prompt versioning, evaluation and content safety. **Nothing here
> runs until Prompt A0's ADRs are written and approved.** Treat it as a design document with
> executable prompts attached.

## Why this sequence looks the way it does

**The golden rule here: build the deterministic half first.** Prompts A1 and A2 stand up the service, its read model, and a rules-based risk score with **no model call at all**. Only A3 introduces inference. That ordering is deliberate — a rules-based baseline is the thing you evaluate the model *against*, and without it "the AI seems good" is the only quality signal you will ever have.

**The second rule: retrieval before generation.** A4 builds tenant-partitioned retrieval and proves isolation before any retrieved content reaches a prompt. Context assembly is the leak surface here, and it is the one your existing five layers do not cover.

## What makes this different from every other feature in this repo

| Concern | Everywhere else | Here |
| --- | --- | --- |
| Leak surface | A row crossing a filter | **Tenant A's history appearing as prose inside tenant B's brief** |
| Isolation mechanism | EF query filter | **Index-level partitioning** — vector search never touches a query filter |
| Idempotency | Don't apply the effect twice | Don't apply it twice **and don't re-spend tokens** |
| Correctness | Deterministic; test asserts equality | Non-deterministic; test asserts **bounds and behaviour** |
| Failure | 500, retry | Plausible, confident, wrong — **and no exception** |
| Local dev | Emulator container | **No emulator exists.** Requires a deterministic stub provider |

---


## Prompt A0 — Write the ADRs, and wait

*The constitution says a new service is an architectural decision, not a feature. This prompt exists to honor that rather than route around it.*

```
SCOPE: Do not write any implementation code. Draft the Architecture Decision Records required before
PlannerPro can gain an AI capability, following the exact format in docs/adr/README.md, using the
NEXT FREE numbers — check docs/adr/ first, do not assume a starting number:

  ADR-00XX  The Advisor bounded context — a ninth service, consumer-fed, advisory-only
  ADR-00XX  Semantic Kernel as the orchestration layer; OpenAI for inference; Foundry for
            evaluation, prompt versioning and content safety
  ADR-00XX  Tenant-partitioned retrieval — pgvector in advisordb, isolation enforced in the index
  ADR-00XX  AI execution is asynchronous, event-driven, and cost-idempotent
  ADR-00XX  AI credits as a plan limit (extends ADR-0017's replicated-quota pattern)
  ADR-00XX  Amendment to ADR-0012 — the local-first exception and the deterministic stub provider

Each must state real alternatives and real consequences, not a rationalization of a decision already
made.

CONSTRAINT: Follow the established ADR shape: Context / Decision / Consequences (Positive, Negative,
Neutral) / Alternatives considered. Relate each to the ADRs it depends on or amends. Reuse existing
patterns where they fit — ADR-0026 in particular should be short, because replicated quotas already
solve it.

RESTRICTION: Do NOT understate the negatives. Specifically, ADR-0022 must state that the Advisor is
ADVISORY ONLY and never mutates another service's data, and say what that forecloses. ADR-0024 must
state plainly that vector similarity search does NOT pass through an EF query filter, so isolation is
enforced somewhere new — this is the sharpest edge of the whole feature. ADR-0025 must address what a
redelivered event costs in tokens. ADR-0027 must be honest that "local-first, zero cloud spend" is
being amended, not preserved, and say what a contributor without an API key actually gets. Do NOT
write an ADR that concludes "and there are no real downsides."

USAGE: Read docs/adr/README.md, ADR-0001, ADR-0012, ADR-0016 and ADR-0017 first — 0016 is the closest
precedent for proposing a new consumer-fed service, and 0017 for the quota pattern.

BEHAVIOR: Draft all six and show them to me. Then STOP. Do not scaffold anything. I want to approve
the decisions before any code exists, and I specifically want to reconsider whether this should be a
ninth service or a capability inside Planning — argue both sides of that in ADR-0022 rather than
assuming the answer.
```

## Prompt A1 — The Advisor service and its read model (no AI)

```
SCOPE: Build PlannerPro.Advisor + PlannerPro.Advisor.Core over advisordb, following the established
service pattern exactly. It is consumer-fed and has one read-only query surface. Build the read model
ONLY — no model calls, no Semantic Kernel, no retrieval.

Consume and maintain, as tenant-scoped local read models:
  - SprintHistory       — from SprintGoalSet, TaskChanged, sprint close events: per (sprint, project)
                          the goal text, final status, planned points, completed points, slip flag
  - EstimateOutcome     — per task: estimated points vs. actual effort signal, for calibration
  - AllocationSnapshot  — from CapacitySet and team membership: who is on which project, at what share
  - ProjectReference    — name, client, colour (the same pattern Planning already uses)

Expose GET /api/t/{tenant}/advisor/history as a read-only diagnostic surface so the read model can be
inspected before anything depends on it.

CONSTRAINT: Follow .claude/rules/backend.md, .claude/rules/tenancy.md and .claude/rules/messaging.md.
Thin host + .Core stack. Every entity implements ITenantScoped. Consumers are inbox-idempotent and
take scope from the envelope. Register advisordb in the AppHost with WithReference/WaitFor.

RESTRICTION: Do NOT call any model. Do NOT add Semantic Kernel, an AI package, or pgvector yet. Do
NOT read another service's database — everything arrives by event. Do NOT let the Advisor write to
any store but advisordb, now or ever. Do NOT use FindAsync.

USAGE: Use the add-endpoint and add-tenant-scoped-entity skills. Use plan mode. Delegate review to
code-reviewer, test-gap-analyzer and tenant-isolation-auditor.

BEHAVIOR: Plan the read-model shape first — specifically, what "slipped" means as a derived fact and
which existing events actually carry enough to compute it. If an event does NOT carry what's needed,
say so rather than inferring it; that's a contract gap to fix in the publisher, not to paper over
here. Wait for approval. Then implement, migrate, test (including the cross-tenant suite), and report
which of the four read models are fully supported by today's events and which are not.
```

## Prompt A2 — The deterministic baseline (still no AI)

*The thing you will measure the model against. Skipping this means never knowing whether the AI helps.*

```
SCOPE: Implement a rules-based sprint risk score in Advisor.Core, with no model involvement. For each
(sprint, project) goal, produce a RiskAssessment: a score, a band (Low/Medium/High), and a list of
plain-language reasons, computed from the read model — historical slip rate for this project, planned
points versus the tenant's overload threshold, share of points in tasks above 8, estimate-calibration
drift, and people allocated across multiple projects.

Expose GET /api/t/{tenant}/advisor/sprints/{sprintId}/risk returning the assessment for every project
in that sprint.

CONSTRAINT: Follow .claude/rules/backend.md. Every weight and threshold is a named, documented
constant in one place — and any that should be per-tenant reads from tenant settings, not a hardcoded
value. The overload threshold in particular is already a per-tenant setting and must not be
reintroduced as a constant.

RESTRICTION: Do NOT call a model. Do NOT invent a scoring formula and present it as validated — state
plainly that the weights are a starting hypothesis to be tuned against real outcomes. Do NOT hide the
reasons behind the score; the reason list is the product, the number is a sort key.

USAGE: Use the add-endpoint skill. Use plan mode. Delegate review to code-reviewer.

BEHAVIOR: Plan the signals and the weighting before writing, and tell me which signals you expect to
be predictive and which you're including on intuition. Wait for approval. Implement with unit tests
over hand-built histories that assert each signal independently moves the score in the expected
direction. Report which signals the current read model can genuinely support.
```

## Prompt A3 — Semantic Kernel, provider abstraction, and the local stub

```
SCOPE: Introduce Semantic Kernel into Advisor.Core as the orchestration layer, with a provider
abstraction and THREE registrations selected by configuration:
  - OpenAI (direct)                — default for development with a key
  - Azure OpenAI via AI Foundry    — the deployment target; model and version from configuration
  - DeterministicStub              — a custom IChatCompletionService returning fixed, structured,
                                     plausible output with NO network call

Wire the stub as the default when no credential is configured, so `aspire run` works for anyone
cloning the repo. Add an AdvisorKernel abstraction so the rest of Advisor.Core never touches an SDK
type directly. Add SK's telemetry to the existing OpenTelemetry pipeline, with TenantId on every span.

CONSTRAINT: Follow ADR-0023 and ADR-0027. Credentials come from Aspire-injected configuration,
user-secrets in development, environment variables in production — never a literal. Model deployment
name and version are configuration, not constants. Follow .claude/rules/aspire.md for resource wiring.

RESTRICTION: Do NOT hardcode an API key, endpoint, deployment name, or model version anywhere — the
secret-guard hook will block it and it should. Do NOT let SK types leak out of the AI layer into the
facade, business layer, or any ServiceModel. Do NOT make the stub throw or return "not configured" —
it must return usable structured output, or local development is broken and nobody will run this
repo. Do NOT call a real model in a unit test.

USAGE: Verify Semantic Kernel package names, the IChatCompletionService surface, and Azure AI Foundry
connection configuration against current Microsoft documentation before writing — this stack moves
faster than any other part of the system and your recalled API names are likely stale. Use plan mode.
Delegate review to code-reviewer.

BEHAVIOR: Plan the abstraction boundary and show me the interface before writing — specifically, what
Advisor.Core sees and what stays behind the boundary. Wait for approval. Implement, then prove the
stub path works with no credentials configured by running `aspire run` and hitting the endpoint. Tell
me exactly what a contributor with no API key can and cannot exercise.
```

## Prompt A4 — Tenant-partitioned retrieval ← the dangerous one

```
SCOPE: Add retrieval over each tenant's own sprint history so the Advisor can ground its reasoning in
specific past sprints. Use pgvector in advisordb (no new datastore — same reasoning as ADR-0016's
choice of Postgres + jsonb). Embed goal text, retro notes, and outcome summaries. Provide a
SearchTenantHistory operation returning the most relevant past sprints for a given goal.

THE CRITICAL PART: vector similarity search does NOT pass through an EF Core global query filter. The
isolation this system relies on everywhere else is absent here. Tenant partitioning must be enforced
in the query itself — a mandatory tenant predicate applied before the similarity operator, in a place
a caller cannot omit or forget. Design it so that a retrieval call without a tenant is impossible to
express, not merely discouraged.

CONSTRAINT: Follow .claude/rules/tenancy.md and ADR-0024. Embeddings are tenant-scoped rows like any
other. Embedding generation is asynchronous and inbox-idempotent — re-embedding on redelivery wastes
money and must not happen.

RESTRICTION: Do NOT expose a retrieval API that accepts an optional tenant. Do NOT rely on the caller
passing the right tenant — take it from ITenantContext inside the retrieval layer itself. Do NOT
build a shared index across tenants with post-filtering; a top-k that filters after ranking silently
returns fewer or zero results AND has already ranked against another tenant's data. Do NOT let
retrieved text reach a prompt in this prompt — that is A5.

USAGE: Use the add-tenant-scoped-entity skill for the embedding entity. Use plan mode. Delegate review
to code-reviewer AND tenant-isolation-auditor — and tell the auditor explicitly that query filters do
not apply on this path, so it does not report a false clean.

BEHAVIOR: Plan the partitioning strategy and state, in writing, exactly where the tenant predicate is
applied and why it cannot be bypassed. Wait for approval. Implement with tests that: two tenants with
deliberately similar goal text never retrieve each other's rows; a retrieval attempted with no tenant
scope fails loudly rather than returning everything; and re-delivery does not re-embed. Report the
result of the similar-text test specifically — that is the one that matters.
```

## Prompt A5 — The risk brief, end to end

```
SCOPE: Produce the AI-authored sprint risk brief. On a SprintOpened event (or an explicit refresh
request), for each project goal in the sprint: assemble context from the read model and tenant-scoped
retrieval, call the model through the AdvisorKernel, and persist a SprintBrief containing a narrative
assessment, the contributing factors, and a suggested action — alongside the deterministic score from
A2, which is retained and shown, not replaced.

Expose GET /api/t/{tenant}/advisor/sprints/{sprintId}/brief and
POST /api/t/{tenant}/advisor/sprints/{sprintId}/brief/refresh (Member+).

Context assembly is the security boundary of this feature. Build it as a single, testable
ContextAssembler that takes ITenantContext and returns a prompt payload — with every input traceable
to a tenant-scoped source.

CONSTRAINT: Follow ADR-0022 and ADR-0025. Generation is asynchronous, triggered by an event, and
inbox-idempotent including cost — a redelivered SprintOpened must not re-spend tokens. Persist the
model identifier, prompt version, and token counts with every brief. Structured output only: define
the response schema and validate it; a brief that fails validation is discarded and retried, not
displayed. Emit BriefGenerated through the outbox so Audit and Billing see it.

RESTRICTION: **The Advisor is advisory only. It must NOT mutate a goal, task, capacity, plan or any
other service's data, under any circumstance, and must not publish an event that causes another
service to do so.** Do NOT put raw un-templated user text into a prompt without treating it as
untrusted input — goal text is authored by users and a goal reading "ignore previous instructions" is
a realistic thing to receive. Do NOT include any data in the prompt that did not come through the
tenant-scoped assembler. Do NOT display a brief that failed schema validation. Do NOT let a model
failure fail the sprint — the deterministic score must still render.

USAGE: Use the add-endpoint skill. Use plan mode. Delegate review to code-reviewer,
test-gap-analyzer and tenant-isolation-auditor.

BEHAVIOR: Plan the context assembly FIRST and show me every field that will enter the prompt and
where each comes from — I want to read that list before any prompt text is written. Wait for
approval. Then implement, and test: a brief for tenant A contains no tenant B data under deliberately
similar histories; a redelivered event produces no second model call; a malformed model response is
discarded; a model outage degrades to the deterministic score rather than an error. Report token cost
per brief on realistic data.
```

## Prompt A6 — AI credits as a plan limit

```
SCOPE: Meter and limit AI usage per tenant, reusing the existing replicated-quota pattern rather than
inventing a mechanism. Advisor publishes AiUsageRecorded (tokens in, tokens out, model, operation) on
every completed generation. Billing consumes it, maintains a per-tenant monthly credit counter, adds
MaxAiCreditsPerMonth to the plan model, and publishes TenantQuotaChanged as it already does. Advisor
enforces against its LOCAL quota snapshot and refuses with 402 { limit: "MaxAiCreditsPerMonth" }.

Add a usage view to the plan/usage page showing credits consumed against the allowance.

CONSTRAINT: Follow ADR-0017 and ADR-0026. No synchronous call to Billing on the generation path.
Counters are inbox-idempotent — a redelivered usage event must not double-count. Credits reset on the
billing period boundary.

RESTRICTION: Do NOT add a synchronous check. Do NOT let a tenant at zero credits lose the
deterministic risk score — that costs nothing and must keep working; only the AI narrative is gated.
Do NOT meter in tokens in the UI; convert to a unit a customer can reason about and state the
conversion. Do NOT let the overshoot window here go unstated — with a variable-cost resource it is
larger and more expensive than it is for project counts, and that needs saying out loud.

USAGE: Use the add-endpoint skill. Use plan mode. Delegate review to code-reviewer and
tenant-isolation-auditor.

BEHAVIOR: Plan the metering contract and — explicitly — size the overshoot window in credits, not in
milliseconds. Wait for approval. Implement and test: a tenant at its limit gets 402 and still sees
the deterministic score; a double-delivered usage event does not double-count; a quota change
propagates. Report the worst-case overshoot in real money.
```

## Prompt A7 — Evaluation, prompt versioning, and safety via Foundry

*The prompt most likely to be skipped, and the one that separates a demo from a product.*

```
SCOPE: Make the Advisor's quality measurable and its prompts governable, using Azure AI Foundry.
  1. Prompt assets versioned in Foundry, referenced by version from configuration — never inline
     strings in code. The version is persisted with every brief (A5) so any output is reproducible.
  2. An evaluation dataset built from real closed sprints in the demo tenants: goal, context, and the
     KNOWN outcome (slipped / delivered).
  3. An evaluation run scoring the AI brief against the A2 deterministic baseline on the same
     dataset, reporting whether the AI is actually better at predicting slippage — and by how much.
  4. Content safety filters on input and output.
  5. A regression gate: an evaluation run that drops below the recorded baseline fails.

CONSTRAINT: Follow ADR-0023. Evaluation runs against a fixed dataset with a pinned model version, or
the result means nothing. Foundry configuration is injected, never hardcoded.

RESTRICTION: Do NOT ship a prompt change without an evaluation run. Do NOT evaluate on data the
prompt was tuned against. Do NOT report a quality improvement without stating the sample size and the
baseline it beat — "the briefs look good" is not a result. Do NOT let the evaluation dataset contain
one tenant's data used to score another's.

USAGE: Verify Azure AI Foundry's evaluation and prompt-asset APIs against current Microsoft
documentation — this surface is new and moves. Use plan mode. Delegate review to code-reviewer.

BEHAVIOR: Plan the dataset construction and the metric before building — specifically, what counts as
a correct prediction and what the baseline scores. Wait for approval. Implement, run the evaluation,
and report the actual numbers including the AI-versus-deterministic comparison. **If the AI does not
beat the rules-based baseline, say so plainly.** That is a real and useful finding, and shipping the
feature anyway would be the wrong call.
```

## Prompt A8 — The Advisor UI

```
SCOPE: Surface the Advisor in the SPA. On the sprint board, a per-project risk band with the
deterministic reasons always visible. A brief panel showing the AI narrative, its contributing
factors, and the suggested action. A refresh control (Member+). A credits indicator when a tenant is
near its limit.

CONSTRAINT: Follow .claude/rules/frontend.md — zoneless, signals, typed services, one interceptor
owning the slug. The brief loads asynchronously and independently of the board; the board never waits
on it.

RESTRICTION: Do NOT present the AI narrative as fact. Label it as an assessment, show the model and
when it was generated, and make the deterministic reasons visible alongside it — a user must be able
to see WHY without trusting the prose. Do NOT offer a control that applies a suggestion
automatically; the Advisor is advisory (ADR-0022) and a one-click "apply" would undo that decision
through the UI. Do NOT block the board render on a missing, stale, or failed brief. Do NOT hardcode
risk thresholds — they come from the API.

USAGE: Use the add-component skill. Use plan mode. Delegate review to code-reviewer and
api-contract-checker.

BEHAVIOR: Plan the component tree and, specifically, how the UI communicates uncertainty — that's the
design problem here, not the layout. Wait for approval. Implement, run ng test and ng build, and
report how the UI behaves when the brief is absent, stale, or generated by a since-superseded prompt
version.
```

---


## Template K — Add an Advisor capability

```
SCOPE: Add <capability> to the Advisor — <what it assesses and what it returns>.

CONSTRAINT: Follow ADR-0022 through ADR-0027. It reads only advisordb, is advisory-only, and is
asynchronous and cost-idempotent. Context assembly goes through the existing tenant-scoped
ContextAssembler. Structured output with a validated schema. Prompt versioned in Foundry.

RESTRICTION: Do NOT mutate anything outside advisordb. Do NOT bypass the ContextAssembler. Do NOT add
a new prompt without adding it to the evaluation dataset. Do NOT ship without a deterministic
fallback for when the model is unavailable or the tenant is out of credits.

USAGE: Use the add-endpoint skill.

BEHAVIOR: Show me every field entering the prompt and its tenant-scoped source before writing prompt
text. Wait for approval. Implement, evaluate against the baseline, and report the numbers and the
per-call token cost.
```

## Template L — Audit an AI surface for tenant leakage

*Run this on any change touching context assembly, retrieval, or caching. Your standard isolation
audit does not cover this path.*

```
SCOPE: Audit <the change / the Advisor> for cross-tenant leakage on the AI path specifically.

CONSTRAINT: The five standard layers DO NOT fully apply here. Check these instead, in addition:
  1. Context assembly — is every field traceable to a tenant-scoped source? Any constant, cache,
     global lookup, or "example" embedded in a prompt template that came from real data?
  2. Retrieval — is the tenant predicate applied BEFORE the similarity operator, not after? Is a
     tenant-less call impossible to express?
  3. Embeddings — are stored vectors tenant-scoped rows, and is the index partitioned?
  4. Caching — does any prompt, response, or embedding cache key include TenantId? A shared prompt
     cache is a cross-tenant leak with a fast path.
  5. Evaluation data — does any dataset mix tenants?
  6. Logs and telemetry — do prompt or completion contents reach logs, and if so, whose?
  7. Prompt injection — can user-authored goal text alter instructions or induce disclosure of
     context from the assembler?

RESTRICTION: This is READ-ONLY — report, do not fix. Do NOT report the standard five layers as clean
and stop; on this path they are largely irrelevant. Do NOT downgrade a finding because the leaked
content would be "just a sentence" — a sentence of another tenant's sprint history is a disclosure.

USAGE: Delegate to tenant-isolation-auditor, telling it explicitly that EF query filters do not apply
to vector search or to prompt assembly.

BEHAVIOR: Report findings ranked by severity, each with the specific path by which one tenant's data
could reach another's output. Then list, item by item, which of the seven checks above you performed
and what you found. Then wait.
```

---

---

## Pro tips

- **Read the plan, not the code.** The highest-leverage thirty seconds you spend is on the plan Claude shows before it writes. Boundary mistakes and missing tenancy stories are obvious there and expensive later.
- **`/clear` between prompts.** Rules and skills reload themselves; stale context is what makes long sessions drift.
- **The tenancy question, every time.** "Where does `TenantId` come from on this path?" has exactly two right answers: the gateway's projected header, or the event envelope. Any third answer is a bug.
- **A leak found in one service is a mechanism failure.** Fix it in `PlannerPro.Shared` and re-run the suite everywhere. Patching the one endpoint means the other seven still have it.
- **When you reuse a Part 2 template three times, promote it to a skill.** That's the signal it's a procedure, not a prompt.
- **The tenancy question, every time:** "Where does `TenantId` come from on this path?" Two right answers, gateway header or event envelope. Any third answer is a bug.
- **"No data" is usually a scope problem, not a query problem.** It reads like a query bug and gets debugged in the wrong place for an hour.
- **Two tenants in dev, always.** One tenant proves nothing; two make a leak visible by eye.
- **Re-run T1 on every EF upgrade.** The assumption it proves is version-sensitive, and nothing will tell you when it stops holding.
- **The deterministic baseline is the product's floor.** If the model is unavailable, the tenant is out of credits, or a response fails validation, the user still gets a useful risk score. Build it first, keep it visible, never let the AI path be load-bearing for the core promise.
- **Context assembly is the security boundary.** Everywhere else in this system a leak is a row. Here it is a paragraph, and no filter, interceptor, or type system will catch it. Review the assembler's field list the way you'd review a migration.
- **"Idempotent" now has a cost dimension.** A redelivered event that re-runs a handler is correct-but-expensive. Check the token spend on every retry path.
- **Evaluate before you believe it.** A7 exists because "the briefs read well" is the easiest way to ship a feature that is worse than arithmetic. If the baseline wins, that's the finding — publish it.
- **Verify the SDK surface, always.** Semantic Kernel and Foundry move faster than anything else in this stack. Recalled API names in this area are stale more often than not.
