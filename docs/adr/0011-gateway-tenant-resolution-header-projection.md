# ADR-0011: Gateway tenant resolution and trusted header projection

- **Status:** Accepted
- **Date:** 2026-07-23
- **Deciders:** Robert Felkins
- **Related:** ADR-0006 (gateway), ADR-0007 (path-based tenancy), ADR-0008 (filters), ADR-0009 (bus scope), ADR-0010 (memberships), ADR-0015 (correlation)

## Context

Something must turn `/api/t/acme/board` into "tenant `9f2c…`, caller is an Admin, and they are entitled to be here." That work needs the tenant record and the caller's membership — both owned by Access (ADR-0010).

Doing it per-service means eight lookups per request and eight implementations of the same security check. Doing it once at the gateway means every service downstream must be able to trust what it is told.

## Decision

**The gateway resolves tenancy once and projects it inward as trusted headers, after stripping any client-supplied copies.**

- Slug → tenant (memory-cached, short TTL) → the caller's `TenantMembership`.
- The gateway **strips** any inbound tenant, actor, role, or correlation header before projecting its own — a client must not be able to assert them.
- It projects: tenant id, tenant slug, membership role, tenant status, actor id, correlation id; and echoes the correlation id on the response for support.
- Services read these into `ITenantContext` via middleware and **never re-resolve a slug**.
- **A caller with no active membership receives 404, never 403** — and the same status, shape, message, and broad timing as a slug that doesn't exist. A 403 confirms the tenant is real.
- Tenants in `Suspended`/`PastDue`/`Cancelled` are read-only at the edge (ADR-0020).

## Consequences

**Positive**
- One implementation of the most security-sensitive check in the product.
- Services are simple: scope arrives ambiently, exactly as it does on the bus (ADR-0009).
- Caching membership at the edge removes a per-request lookup from every service.

**Negative**
- **Every service unconditionally trusts these headers.** If a service is reachable without traversing the gateway, tenant isolation is gone — not degraded, gone. Network-level enforcement is therefore a hard deployment requirement, and it is currently unimplemented (tracked in `docs/design/build-plan-and-risks.md` as a top risk).
- Cached membership means a revoked member or a changed role stays effective for the cache TTL. Invalidation on membership change is required, and the TTL is a deliberate exposure window that must be short and stated.
- The gateway now needs read access to tenant and membership data — a dependency on Access on every request path.

**Neutral**
- Header names are an internal contract between the gateway and `PlannerPro.Shared`; they are not public API.

## Alternatives considered

- **Tenant claim in a signed token.** Cryptographically self-verifying, so a service could trust it without trusting the network — a real advantage over headers. Rejected for now because it makes membership changes take effect only on token refresh, which is the wrong trade for revocation. **This is the most likely of all these ADRs to be superseded**, and would pair naturally with ADR-0021.
- **Per-service resolution.** Rejected: eight implementations, eight chances to leak, plus a lookup per service per request.
- **Trust a client-supplied header.** Not a real option; listed because it is what happens by accident if stripping is ever removed.
