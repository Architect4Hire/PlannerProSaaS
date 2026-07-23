# PlannerPro Documentation

The design, decisions, and prompts behind PlannerPro — a **multi-tenant, white-label SaaS** for sprint planning across parallel projects, built as event-driven microservices on **Aspire + ASP.NET Core + Angular (.NET 10)**, driven with Claude Code under the SCRUB framework.

Everything the docs reference — the constitution ([`../CLAUDE.md`](../CLAUDE.md)) and the toolkit ([`../.claude/`](../.claude/): rules, skills, subagents, hooks) — already lives in the repo.

> **State of play.** `src/` does not exist yet. This documentation set describes the **target** architecture that [`prompts/scrub-prompts.md`](./prompts/scrub-prompts.md) builds. The ADRs are decisions already taken; the design docs are the plan; the developer guides and pattern deep dives describe mechanisms as designed rather than as observed. Docs that will need revision once code exists say so at the top.

## Contents

### Design & architecture ([`design/`](./design/))

| Document | What's inside |
| --- | --- |
| [High-Level Design](./design/high-level-design.md) | The target architecture — eight bounded contexts, the tenancy spine, C4 views, runtime flows, cross-cutting concerns, data architecture, known gaps. Living document. |
| [Build Plan & Risks](./design/build-plan-and-risks.md) | Sequencing and a ranked risk register. Why the prompt order is what it is, the two unproven assumptions, what "done" means per phase, and what *not* to do. |
| [Product Completeness](./design/product-completeness.md) | The *product* roadmap: what a customer can do today vs. what a sellable SaaS needs — payment, email, onboarding, reporting — each named to its service, with a value-tiered build order. |

### Decisions ([`adr/`](./adr/))

Twenty-one records covering the load-bearing decisions. See the [index and reading paths](./adr/README.md).

**If you read three:** [ADR-0001](./adr/0001-microservices-database-per-service.md) (why eight services), [ADR-0008](./adr/0008-shared-schema-tenantid-discriminator.md) (how tenants are separated), [ADR-0009](./adr/0009-tenant-scope-from-event-envelope.md) (the mechanism with no single-tenant counterpart, and the one most likely to break).

<details>
<summary><b>ADR index</b> (21 records)</summary>

| # | Title | Status |
| --- | --- | --- |
| [0001](./adr/0001-microservices-database-per-service.md) | Microservices with database-per-service | Accepted |
| [0002](./adr/0002-event-driven-integration-over-service-bus.md) | Event-driven integration over Azure Service Bus | Accepted |
| [0003](./adr/0003-hand-rolled-transactional-outbox.md) | Hand-rolled transactional outbox | Accepted |
| [0004](./adr/0004-idempotent-inbox-at-least-once-delivery.md) | Idempotent inbox over at-least-once delivery | Accepted |
| [0005](./adr/0005-thin-host-core-layered-library.md) | Thin host + `.Core` layered library | Accepted |
| [0006](./adr/0006-single-api-gateway-yarp.md) | Single YARP gateway as the only public door | Accepted |
| [0007](./adr/0007-path-based-tenancy-slug-routing.md) | Path-based tenancy (`/t/{slug}`) | Accepted |
| [0008](./adr/0008-shared-schema-tenantid-discriminator.md) | Shared schema, `TenantId` discriminator, automatic filters | Accepted |
| [0009](./adr/0009-tenant-scope-from-event-envelope.md) | Tenant scope from the event envelope | Accepted |
| [0010](./adr/0010-global-identity-tenant-membership.md) | Global identity with tenant membership | Accepted |
| [0011](./adr/0011-gateway-tenant-resolution-header-projection.md) | Gateway tenant resolution & header projection | Accepted |
| [0012](./adr/0012-aspire-local-first-emulators.md) | Aspire local-first topology with emulators | Accepted |
| [0013](./adr/0013-contracts-leaf-tenant-envelope.md) | `Contracts` as a leaf with a mandatory tenant envelope | Accepted |
| [0014](./adr/0014-cross-service-read-model-strategy.md) | Cross-service read models over query composition | Accepted |
| [0015](./adr/0015-correlation-causation-identifiers-on-events.md) | Correlation & causation identifiers on events | Accepted |
| [0016](./adr/0016-audit-bounded-context-bus-fed-support-trail.md) | The Audit bounded context | Accepted |
| [0017](./adr/0017-replicated-quota-local-limit-enforcement.md) | Replicated quotas with local limit enforcement | Accepted |
| [0018](./adr/0018-plan-model-now-stripe-later.md) | Model plans now; wire Stripe later | Accepted |
| [0019](./adr/0019-white-label-css-custom-properties.md) | White-label via CSS custom properties | Accepted |
| [0020](./adr/0020-suspended-tenants-read-only.md) | Suspended tenants become read-only | Accepted |
| [0021](./adr/0021-cookie-at-edge-internal-token.md) | Cookie at the edge, trusted headers inward | Accepted |

</details>

### Building the system ([`developer/`](./developer/), [`prompts/`](./prompts/))

| Document | What's inside |
| --- | --- |
| [Tracing a Slice: Tenant Provisioning](./developer/tracing-a-slice-tenant-provisioning.md) | One request across every layer and four services — anonymous endpoint, multi-entity transaction, outbox, dispatcher, envelope scope, idempotent consumers, audit trail. **Read this first** to see how the pieces fit. |
| [Adding an Endpoint by Hand](./developer/adding-an-endpoint-manually.md) | The full controller → repository slice without Claude Code — including the tenancy story you write down *before* coding. |
| [Adding Seed Data](./developer/adding-seed-data.md) | Development data across eight databases and several tenants, and why seeding is one of only three legitimate filter bypasses. |
| [Tracing the Outbox: `ProjectCreated`](./developer/tracing-the-outbox-project-created.md) | One event from the business method that builds it to the four services that react, and the quota loop closing behind it. |
| [Working with AI Assistants](./developer/working-with-ai-assistants.md) | How Claude Code and GitHub Copilot are both supported: what's shared, what's generated, and what actually enforces anything. |
| [Pattern Deep Dives](./developer/patterns/README.md) | Sixteen mechanisms — layering, tenant isolation, bus scoping, outbox/inbox, gateway, auth, contracts, audit, limits, white-label, caching, concurrency, Aspire, errors, the frontend seam. |
| [SCRUB Prompts](./prompts/scrub-prompts.md) | **The single build sequence.** *Part 1:* twenty-three prompts, 0 → 22, in order — services, app, public front door, deployment — with prove-it-first steps inside Prompts 2 and 3 and gates at 8 and 11. *Part 2:* ten operational templates. *Part 3:* the proposed Sprint Advisor, designed but not scheduled. |

## Where to start

- **New to the project?** [High-Level Design](./design/high-level-design.md), then [Tracing a Slice](./developer/tracing-a-slice-tenant-provisioning.md), then skim the [ADRs](./adr/README.md).
- **Building it from scratch?** Start a Claude Code session at the repo root and run [SCRUB Prompts](./prompts/scrub-prompts.md), Part 1, Prompt 0 — Prompt 2 opens with a prove-it-first step that can save a week — don't skip it.
- **Adding a feature by hand?** [Adding an Endpoint by Hand](./developer/adding-an-endpoint-manually.md).
- **Working on tenancy?** [Tenant Isolation](./developer/patterns/tenant-isolation-defense-in-depth.md) and [Tenant Scope on the Bus](./developer/patterns/tenant-scope-on-the-bus.md), then [SCRUB Prompts](./prompts/scrub-prompts.md).
- **Reviewing where it stands?** [Build Plan & Risks](./design/build-plan-and-risks.md).
- **Deciding what to build next?** [Product Completeness](./design/product-completeness.md).
- **Investigating a suspected leak?** Stop and use [Template J](./prompts/scrub-prompts.md), plus `@tenant-isolation-auditor`.

## Relationship to the earlier plan

`multitenancy-plan.md` proposed evolving the single-tenant application in place — shared database, query filters, one deployable. Its **isolation architecture (§4)** and **backfill migration (§9)** are superseded by ADR-0001, ADR-0008 and ADR-0009. Its **domain model (§3)**, **role matrix (§5)**, **plans and limits (§6)**, **white-label scope (§7)** and **out-of-scope list (§12)** remain load-bearing and are the product input to everything here.
