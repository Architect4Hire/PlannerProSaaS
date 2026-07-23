# ADR-0008: Shared schema with a `TenantId` discriminator and automatic query filters

- **Status:** Accepted
- **Date:** 2026-07-23
- **Deciders:** Robert Felkins
- **Related:** ADR-0001 (database-per-service), ADR-0009 (tenant scope on the bus), ADR-0010 (global identity), ADR-0011 (gateway resolution), `.claude/rules/tenancy.md`

## Context

Each service owns a database (ADR-0001). Within that database, many tenants' rows coexist. The isolation model must answer: how are they separated, and what stops a missed filter from becoming a data breach?

The isolation strategies are database-per-tenant-per-service (strongest, operationally brutal at eight services × N tenants), schema-per-tenant (a middle ground with migration pain proportional to tenant count), and a shared schema with a discriminator column (cheapest to run, requires disciplined defense).

The migration cost matters here in a way it doesn't in a monolith: a per-tenant database in an eight-service system means eight databases per customer, and every migration multiplies accordingly.

## Decision

**Tenant rows share a schema within each service's database, separated by a `Guid TenantId` discriminator, with EF Core global query filters applied automatically.**

- Every tenant-scoped entity implements `ITenantScoped { Guid TenantId { get; set; } }`.
- The base `DbContext` in `PlannerPro.Shared` **reflects over every `ITenantScoped` entity type in `OnModelCreating`** and applies `HasQueryFilter(e => e.TenantId == _tenant.TenantId || _tenant.BypassFilters)`. Adding an entity cannot forget the filter, because nobody adds one by hand.
- The filter **closes over the injected `ITenantContext`** so it re-evaluates per request rather than baking in the first tenant the model ever saw.
- A `TenantSaveChangesInterceptor` stamps `TenantId` on `Added` entities and throws `CrossTenantWriteException` when a `Modified`/`Deleted` entity's `TenantId` doesn't match the current scope — catching the paths a query filter can't see (tracked entities, attached graphs, raw SQL).
- **`TenantId` is a `Guid`, never an `int`** — not guessable, no collisions across environments or imports.
- **`TenantId` is never client-supplied.** It arrives from the gateway's projected header (ADR-0011) or the event envelope (ADR-0009), and appears on no ViewModel.
- **Every uniqueness constraint is tenant-scoped:** `(TenantId, Slug)`, never `(Slug)`.
- Outbox and inbox rows carry `TenantId` like any other row.

## Consequences

**Positive**
- One migration set per service regardless of tenant count — the operational property that makes this affordable at eight services.
- Onboarding a tenant is an insert, not a provisioning job.
- Defense is automatic by default: reflection applies the filter, the interceptor stamps the write, and neither depends on an author remembering.

**Negative**
- **A missed filter is a data breach, not a bug.** The entire model rests on layered defense (query filter, interceptor, gateway resolution, role filters, tests) precisely because no single layer is trustworthy alone.
- Known holes must be closed by discipline: `FindAsync` returns tracked entities **without applying filters**; `IgnoreQueryFilters()` is legal only in a bypass context; a globally unique index leaks the existence of other tenants' rows through constraint violations. These are enforced by `.claude/rules/tenancy.md`, the `tenancy-guard.sh` hook, and the `tenant-isolation-auditor` subagent.
- **The load-bearing assumption is EF-version-sensitive:** a query filter closing over an injected scoped service, interacting with the compiled-model cache and any context pooling. This must be proven with a test before anything is built on it, and re-proven on EF upgrade.
- Noisy-neighbour effects are shared within a service's database; there is no per-tenant resource isolation.
- A per-tenant data export or hard delete is a query, not a `DROP DATABASE`.

**Neutral**
- Tenants share a database *within* a service while services never share one. Two different boundaries, deliberately different mechanisms.

## Alternatives considered

- **Database per tenant per service.** The strongest isolation, trivially correct export and delete, and natural per-tenant resource limits. Rejected: eight databases per customer, migrations multiplied by tenant count, and connection-pool pressure that grows with the customer list. Reconsider for a small number of large enterprise tenants — a hybrid (shared by default, dedicated on request) is the natural escape hatch and this decision does not block it.
- **Schema per tenant.** Middle ground, but migration pain still scales with tenant count and EF support is awkward.
- **Hand-written `WHERE TenantId = @x` on every query.** Rejected outright: correctness depending on every author remembering, every time, across eight services and an agent.
