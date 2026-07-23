---
paths:
  - src/PlannerPro.Shared/**
  - src/PlannerPro.Contracts/**
  - src/PlannerPro.Gateway/**
  - src/PlannerPro.*.Core/Data/**
  - src/PlannerPro.*.Core/Managers/Models/Domain/**
  - src/PlannerPro.*/Consumers/**
---
# Tenancy rules — isolation in a database-per-service system

PlannerPro is multi-tenant. Every service owns its own database, and **each of those databases holds
rows for many tenants**, separated by a `TenantId` discriminator. A missed filter is not a bug, it is
a data breach — so isolation is layered deliberately, and no single mistake should be enough.

## Where `TenantId` comes from — there are exactly two answers

1. **On an HTTP path:** the gateway resolved the slug, checked membership, and projected a trusted
   header. Middleware reads it into `ITenantContext`.
2. **On a bus path:** the integration event's envelope carries it, and the Service Bus processor host
   establishes scope from it *before* invoking the consumer.

**Any third source is a security bug.** Not a request body, not a query string, not a route value a
service read itself, not a JWT claim the gateway didn't mint. If you find yourself writing
`viewModel.TenantId`, stop.

## The five layers

- **Layer 1 — `ITenantContext`** (scoped): `TenantId`, `Slug`, `Role`, `Plan`, `Status`,
  `IsResolved`, `BypassFilters`. Injected into the `DbContext`. Two non-request implementations
  exist and only these may bypass: `SystemTenantContext` (migrate, seed, platform admin) and
  `DesignTimeTenantContext` (`dotnet ef`).
- **Layer 2 — Gateway resolution.** Slug → tenant → the caller's `TenantMembership`, cached briefly.
  Client-supplied tenant/actor/correlation headers are **stripped**; the gateway's own are projected.
- **Layer 3 — Global query filters.** The base `DbContext` reflects over every `ITenantScoped` entity
  and applies the filter automatically in `OnModelCreating`. The filter closes over the **injected**
  `ITenantContext` so it re-evaluates per request. Adding an entity should not require remembering to
  add a filter — if it does, the mechanism is wrong.
- **Layer 4 — `TenantSaveChangesInterceptor`.** Stamps `TenantId` on `Added` entities; throws
  `CrossTenantWriteException` when a `Modified`/`Deleted` entity's `TenantId` doesn't match the
  current tenant. This is what catches the paths a query filter can't see — tracked entities,
  attached graphs, raw SQL projections.
- **Layer 5 — Role filters.** `RequireTenantRole(TenantRole minimum)` for tenant surfaces,
  `RequirePlatformAdmin` for the platform surface.

Plus tests: the cross-tenant suite, and a reflection test asserting every `ITenantScoped` type has a
filter.

## Hard rules

- **`TenantId` is a `Guid`,** never an `int`. Not guessable, no collisions across environments or
  imports.
- **Never `FindAsync` (or `Find`) on a tenant-scoped entity.** They return tracked entities
  **without applying query filters** — precisely the hole this architecture exists to close. Use
  `FirstOrDefaultAsync` / `SingleOrDefaultAsync`, always. This is the single most likely way this
  system leaks data.
- **Never `IgnoreQueryFilters()` outside a bypass context.** Migration, seeding, and the
  platform-admin surface, each narrow and commented. Anywhere in a facade, business, or repository
  method serving a normal request, it's a defect.
- **404, never 403.** A caller who isn't a member of a tenant — or who names a resource id belonging
  to another tenant — gets a plain 404. A 403 confirms the thing exists. So does a distinctive error
  message, a validation failure naming an internal field, or a materially different response shape.
- **Every uniqueness constraint is tenant-scoped.** `(TenantId, Slug)`, not `(Slug)`. A global unique
  index on tenant-scoped data leaks the existence of other tenants' rows through constraint
  violations, and it is the rule most often missed.
- **Every integration event carries `TenantId`.** Publishing one without it is a bug even if today's
  consumer doesn't read it — the consumer has no other way to establish scope. A message arriving
  without one is dead-lettered, not processed under a guessed scope.
- **Outbox and inbox rows carry `TenantId`** too. They're rows in a tenant's database like any other.
- **Blob names are tenant-prefixed:** `{tenantId}/{taskId}/{guid}.ext`. Otherwise a guessed id yields
  another tenant's file, and per-tenant storage accounting is impossible. Verify the (filtered)
  attachment row before streaming any blob.
- **`TenantId` is a tag on every span and log scope.** Debugging a multi-tenant system without it is
  guesswork.

## The deliberate exception: global identity

`accessdb` is the one database with mixed scoping. **ASP.NET Identity tables are global and
deliberately unfiltered** — one account per email across the whole platform, so a consultant with
several client orgs is one user, and a tenant switcher comes for free. Tenancy is expressed by
`TenantMembership` rows, which *are* scoped.

Consequences worth holding in mind: `IsAdmin` and `DefaultCapacityPoints` are **per-tenant** and live
on the membership, not the user; a separate `IsPlatformAdmin` flag on the user exists for staff. Any
query enumerating users for a tenant goes through `TenantMemberships` joined to `Users` — never
`db.Users` directly, which would enumerate the entire platform.

Comment this exception where it lives. Someone will eventually try to "fix" it.

## Known traps, stated plainly

1. **A consumer has no HTTP request.** Scope must come from the envelope, established centrally by
   the processor host. A consumer that resolves its own tenancy is doing the wrong thing.
2. **Design-time and startup run outside a request** — `dotnet ef` and the migrate/seed path need
   their bypass contexts, or they'll silently see nothing.
3. **EF caches the compiled model per context type.** The filter must close over the injected context
   instance so it re-evaluates per request rather than baking in the first tenant it ever saw.
4. **Required navigations plus filters** produce EF warnings when a filtered principal has unfiltered
   dependents. Keep filters consistent across the whole graph rather than silencing the warning.
5. **Suspension is read-only, not lockout.** Suspended, PastDue and Cancelled tenants keep reading and
   exporting; writes are refused at the edge. Better product behavior, less support load.

## When you add anything

State the tenancy story out loud: which entity is `ITenantScoped`, where `TenantId` comes from on
each path, and what a caller from another tenant sees. If the answer is "this doesn't need one," say
why — don't leave it unstated. Then run `@tenant-isolation-auditor`.
