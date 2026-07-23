# Tenant Isolation: Defense in Depth

**Decided by:** [ADR-0008](../../adr/0008-shared-schema-tenantid-discriminator.md), [ADR-0009](../../adr/0009-tenant-scope-from-event-envelope.md), [ADR-0010](../../adr/0010-global-identity-tenant-membership.md), [ADR-0011](../../adr/0011-gateway-tenant-resolution-header-projection.md)
**Rules:** [`.claude/rules/tenancy.md`](../../../.claude/rules/tenancy.md)
**Subagent:** `@tenant-isolation-auditor`

---

## The problem

Eight services, each owning a database. Within each database, many tenants' rows coexist, separated by a `Guid TenantId` column. Nothing physical stops a query from returning another tenant's rows — only a filter does. And a missed filter is not a bug that produces a wrong number; it is one customer reading another customer's data.

So the design assumes **every individual layer will eventually be bypassed**, and no single mistake should be sufficient.

## Two legal sources of `TenantId`

This is the shortest useful summary of the whole model:

| Path | Source | Established by |
| --- | --- | --- |
| HTTP | Gateway-projected trusted header | `TenantContextMiddleware` |
| Bus | The event envelope | `ServiceBusProcessorHost` |

**There is no third.** Not a request body, not a query string, not a route value a service parsed itself, not a JWT claim the gateway didn't mint. If you are writing `viewModel.TenantId`, stop — that is the bug this whole document exists to prevent.

## The five layers

**Layer 1 — `ITenantContext` (scoped).** Holds `TenantId`, `Slug`, `Role`, `Plan`, `Status`, `IsResolved`, `BypassFilters`. Injected into the `DbContext`. Two non-request implementations exist and are the *only* legal bypasses: `SystemTenantContext` (migrate, seed, platform admin) and `DesignTimeTenantContext` (`dotnet ef`).

**Layer 2 — Gateway resolution.** Slug → tenant → the caller's membership, cached briefly. Client-supplied trust headers are stripped; the gateway's own are projected. See [API Gateway, Edge & Tenant Resolution](./api-gateway-edge-and-tenant-resolution.md).

**Layer 3 — Automatic query filters.** The base `DbContext` **reflects over every `ITenantScoped` entity type** in `OnModelCreating` and applies the filter. Nobody writes a filter by hand, so nobody can forget one. The filter closes over the *injected* context so it re-evaluates per request.

> ⚠️ **Assumption, not verified fact.** That the filter re-evaluates per request — rather than capturing the first tenant the compiled model ever saw — is EF-version-sensitive and interacts with context pooling. Prompt 2 requires proving it with a two-tenant test before anything is built on it. If it fails, every tenant sees tenant one's data and nothing throws.

**Layer 4 — `TenantSaveChangesInterceptor`.** Stamps `TenantId` on `Added` entities from the context. Throws `CrossTenantWriteException` when a `Modified`/`Deleted` entity's `TenantId` doesn't match current scope. This catches what a query filter cannot see: tracked entities, attached graphs, raw SQL projections.

**Layer 5 — Role filters.** `RequireTenantRole(minimum)` on tenant surfaces; `RequirePlatformAdmin` on the platform surface.

**Plus tests.** The cross-tenant suite (every scoped read and write from tenant B against tenant A's ids returns 404 and mutates nothing), and a reflection test enumerating every `ITenantScoped` type and asserting a filter exists.

## The known holes, and what closes each

| Hole | Why it leaks | Closed by |
| --- | --- | --- |
| `FindAsync` / `Find` | Returns **tracked entities without applying query filters** | Rule, `tenancy-guard.sh` warning, subagent check, build-failing test |
| `IgnoreQueryFilters()` | Turns Layer 3 off entirely | Legal only in a bypass context; must be narrow and commented |
| Globally unique index | A constraint violation confirms another tenant's row exists | Every uniqueness constraint includes `TenantId` |
| `db.Users` enumeration | Identity is global (ADR-0010) — enumerates the whole platform | Query `TenantMemberships` joined to `Users` |
| Cache key without `TenantId` | A cross-tenant leak with a fast path | See [Read-Through Caching](./read-through-caching.md) |
| Unprefixed blob name | A guessed id yields another tenant's file | `{tenantId}/{taskId}/{guid}.ext` |
| Raw SQL / `FromSqlRaw` | Filters do not apply | Explicit tenant predicate required |
| 403 instead of 404 | The status code confirms the tenant exists | 404 with identical shape, message and broad timing |

## The deliberate exception

`accessdb` holds **global** Identity tables (unfiltered by design — ADR-0010) alongside tenant-scoped tenants, memberships, invitations, settings and branding. This is the one place in the system where "this table has no filter" is correct, and it is therefore the most likely place someone helpfully adds one, or copies the pattern somewhere it doesn't belong. It is commented in code and called out in the rules — which is documentation, a weaker mitigation than a mechanism, and worth knowing is weaker.

## Standing rules

- `TenantId` is a `Guid`, never client-supplied, absent from every ViewModel.
- `FirstOrDefaultAsync`, never `FindAsync`.
- Every uniqueness constraint is `(TenantId, …)`.
- 404, never 403 — and the same shape as a nonexistent tenant.
- State the tenancy story for anything you add: where `TenantId` comes from, which entities are scoped, what another tenant's caller sees.
- Run `@tenant-isolation-auditor` on any new entity, endpoint, consumer, query, migration or route.
