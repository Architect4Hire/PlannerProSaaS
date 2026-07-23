# ADR-0006: Single YARP gateway as the only public door

- **Status:** Accepted
- **Date:** 2026-07-23
- **Deciders:** Robert Felkins
- **Related:** ADR-0007 (path-based tenancy), ADR-0011 (tenant resolution & header projection), ADR-0021 (cookie at the edge), `.claude/rules/gateway.md`

## Context

With eight services, the browser needs one address, not eight. More importantly, several concerns must be enforced in exactly one place or they will be enforced inconsistently in eight: authentication, tenant resolution, header stripping, correlation minting, per-tenant rate limiting, and the read-only mode for suspended tenants.

Doing any of these per-service means eight chances to get it wrong, and a security control that is only as strong as its weakest implementation.

## Decision

**`PlannerPro.Gateway` (YARP) is the only public entry point, and the only place tenancy is resolved.**

- The Angular app talks to the gateway and nothing else; services are never exposed to the browser.
- Routing is by **Aspire resource name** (`http://planning`), resolved by service discovery — the one sanctioned place a service name appears in configuration.
- The gateway validates the caller, resolves `/api/t/{slug}` to a tenant and membership, strips client-supplied trust headers, and projects its own (ADR-0011).
- Edge cross-cutting only: rate limiting (including per-tenant), CORS, correlation propagation, read-only enforcement for suspended tenants (ADR-0020). **No business logic.**
- A service endpoint with no gateway route is internal-only by design (Notifications has none).

## Consequences

**Positive**
- One place to enforce authentication, tenancy, and rate limiting — one place to audit them.
- The gateway's public paths are a stable contract; service boundaries behind it can move without the client noticing.
- Services can assume they are being called by a trusted edge, which simplifies every one of them.

**Negative**
- A single point of failure and a potential bottleneck.
- **A single point of compromise.** Every service trusts the gateway's projected headers; if the gateway can be bypassed on the network, tenant isolation collapses. Network-level enforcement that services are unreachable except via the gateway is a deployment requirement, not an optional hardening step — and it is currently unaddressed (see `docs/design/build-plan-and-risks.md`).
- Gateway configuration becomes a thing to keep in step with service routes.

**Neutral**
- Routing by resource name couples the gateway config to Aspire naming — intended, since discovery reads it.

## Alternatives considered

- **Direct service exposure with per-service auth.** Rejected: eight implementations of tenant resolution is eight chances to leak.
- **A commercial API gateway / service mesh.** More features, and the right answer at scale. Rejected as disproportionate here and harder to run locally with no cloud spend.
- **BFF per client.** Only one client exists; premature.
