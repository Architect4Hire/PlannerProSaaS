# Architecture Decision Records

This directory records the **load-bearing architectural decisions** for PlannerPro — the ones that are expensive to reverse and that every contributor (human or agent) needs to understand and honor. Each ADR captures the *context* that forced a decision, the *decision* itself, its *consequences*, and the *alternatives* that were weighed and rejected.

ADRs are immutable once **Accepted**: to change a decision, add a new ADR that **supersedes** the old one (and mark the old one `Superseded by ADR-NNNN`). This keeps the history of *why* intact.

**Where to start.** ADR-0001 explains why there are eight services at all, and ADR-0008 and ADR-0009 together are the tenancy spine — if you read only three, read those.

## Index

| # | Title | Status |
| --- | --- | --- |
| [0001](./0001-microservices-database-per-service.md) | Microservices with database-per-service | Accepted |
| [0002](./0002-event-driven-integration-over-service-bus.md) | Event-driven integration over Azure Service Bus | Accepted |
| [0003](./0003-hand-rolled-transactional-outbox.md) | Hand-rolled transactional outbox | Accepted |
| [0004](./0004-idempotent-inbox-at-least-once-delivery.md) | Idempotent inbox over at-least-once delivery | Accepted |
| [0005](./0005-thin-host-core-layered-library.md) | Thin host + `.Core` layered library; one-way acyclic references | Accepted |
| [0006](./0006-single-api-gateway-yarp.md) | Single YARP gateway as the only public door | Accepted |
| [0007](./0007-path-based-tenancy-slug-routing.md) | Path-based tenancy (`/t/{slug}`) rather than subdomains | Accepted |
| [0008](./0008-shared-schema-tenantid-discriminator.md) | Shared schema with a `TenantId` discriminator and automatic query filters | Accepted |
| [0009](./0009-tenant-scope-from-event-envelope.md) | Tenant scope for consumers is established from the event envelope | Accepted |
| [0010](./0010-global-identity-tenant-membership.md) | Global identity with tenant membership | Accepted |
| [0011](./0011-gateway-tenant-resolution-header-projection.md) | Gateway tenant resolution and trusted header projection | Accepted |
| [0012](./0012-aspire-local-first-emulators.md) | Aspire local-first topology with emulators | Accepted |
| [0013](./0013-contracts-leaf-tenant-envelope.md) | `Contracts` as a leaf library with a mandatory tenant envelope | Accepted |
| [0014](./0014-cross-service-read-model-strategy.md) | Cross-service read models over query composition | Accepted |
| [0015](./0015-correlation-causation-identifiers-on-events.md) | Correlation and causation identifiers on integration events | Accepted |
| [0016](./0016-audit-bounded-context-bus-fed-support-trail.md) | The Audit bounded context — a bus-fed support trail | Accepted |
| [0017](./0017-replicated-quota-local-limit-enforcement.md) | Replicated quotas with local limit enforcement | Accepted |
| [0018](./0018-plan-model-now-stripe-later.md) | Model plans and limits now; wire Stripe later | Accepted |
| [0019](./0019-white-label-css-custom-properties.md) | White-label theming through CSS custom properties | Accepted |
| [0020](./0020-suspended-tenants-read-only.md) | Suspended tenants become read-only, not locked out | Accepted |
| [0021](./0021-cookie-at-edge-internal-token.md) | Cookie at the browser edge, internal token behind the gateway | Accepted |

## Reading paths

- **"Why eight services and not one?"** → [0001](./0001-microservices-database-per-service.md), then [0005](./0005-thin-host-core-layered-library.md).
- **"How is one tenant kept out of another's data?"** → [0008](./0008-shared-schema-tenantid-discriminator.md), [0009](./0009-tenant-scope-from-event-envelope.md), [0011](./0011-gateway-tenant-resolution-header-projection.md), [0010](./0010-global-identity-tenant-membership.md).
- **"How do services talk?"** → [0002](./0002-event-driven-integration-over-service-bus.md), [0003](./0003-hand-rolled-transactional-outbox.md), [0004](./0004-idempotent-inbox-at-least-once-delivery.md), [0013](./0013-contracts-leaf-tenant-envelope.md), [0014](./0014-cross-service-read-model-strategy.md).
- **"How does it work as a business?"** → [0017](./0017-replicated-quota-local-limit-enforcement.md), [0018](./0018-plan-model-now-stripe-later.md), [0019](./0019-white-label-css-custom-properties.md), [0020](./0020-suspended-tenants-read-only.md).
- **"What's most likely to change?"** → [0011](./0011-gateway-tenant-resolution-header-projection.md) and [0021](./0021-cookie-at-edge-internal-token.md) (a signed internal token supersedes both), then [0017](./0017-replicated-quota-local-limit-enforcement.md) if quota overshoot proves commercially unacceptable.

## Relationship to the superseded plan

`multitenancy-plan.md` (2026-07-22) proposed evolving the single-tenant modular application in place: shared database, `TenantId` discriminator, EF query filters, one deployable. That plan's **isolation architecture (§4)** and **single backfill migration (§9)** are superseded by ADR-0001, ADR-0008 and ADR-0009 — there is no longer a single `DbContext`, and there is no existing data to backfill.

The rest of that plan remains load-bearing and is the domain input to these ADRs: the entity model (§3), the role matrix (§5), plans and limits (§6), white-label scope (§7), and the out-of-scope list (§12). Read it for *what* the product is; read these for *how* it is built.

## Statuses

- **Proposed** — under discussion; not yet binding.
- **Accepted** — the current, binding decision.
- **Superseded** — replaced by a later ADR (named in the header).
- **Deprecated** — no longer relevant, not replaced.

## Template

```markdown
# ADR-NNNN: <short decision title>

- **Status:** Proposed | Accepted | Superseded by ADR-XXXX | Deprecated
- **Date:** YYYY-MM-DD
- **Deciders:** <who>
- **Related:** ADR-XXXX, docs/design/high-level-design.md §N, CLAUDE.md

## Context
<The forces at play: requirements, constraints, the problem that demands a decision.>

## Decision
<The decision, stated plainly and actively: "We will …".>

## Consequences
**Positive** / **Negative** / **Neutral** — what becomes easier, what becomes harder, what we accept.

## Alternatives considered
<Each rejected option and *why* it lost.>
```

See also: [`docs/design/high-level-design.md`](../design/high-level-design.md) (design narrative), [`CLAUDE.md`](../../CLAUDE.md) (enforceable ruleset), [`docs/design/build-plan-and-risks.md`](../design/build-plan-and-risks.md) (sequencing & risk register).
