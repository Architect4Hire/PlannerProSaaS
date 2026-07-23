# Pattern Deep Dives

*Sixteen mechanisms, each explained once.*

The other developer docs walk a **task** end to end (add an endpoint, seed a database). These walk a **mechanism** end to end — the thing that recurs across every feature slice, explained in enough depth to extend or debug it without re-deriving it. Each links to the ADR that made the call (the *why* — these docs don't re-argue it) and closes with the standing rules from `.claude/rules/` that keep the pattern honest.

> **Read this first.** `src/` does not exist yet. These docs describe the mechanisms **as designed and as the SCRUB prompt library builds them** — not as observed in running code. Where a doc states a behaviour that is an *assumption* rather than a verified fact, it says so explicitly. Once the system is built, these should be revised to cite real files and line numbers, and the assumption flags removed or turned into findings.

## Index

| Doc | Pattern | Read this when… |
| --- | --- | --- |
| [Layered Service Architecture](./layered-service-architecture.md) | Thin host + `.Core` stack, three model types, mappers | You're unsure which layer a piece of logic belongs in, or a model type is leaking. |
| [Tenant Isolation: Defense in Depth](./tenant-isolation-defense-in-depth.md) | **The spine.** Five layers, two legal sources of `TenantId`, the known holes | You're adding anything that touches tenant data — which is nearly everything. |
| [Tenant Scope on the Bus](./tenant-scope-on-the-bus.md) | **The hard one.** Establishing scope for a consumer that has no HTTP request | You're writing or debugging a consumer, or a consumer "works" but writes nothing. |
| [Transactional Outbox & Inbox](./transactional-outbox-and-inbox.md) | Reliable at-least-once messaging | An event isn't arriving, a consumer double-applies, or you're adding a publish path. |
| [Database-per-Service & Data Ownership](./database-per-service-and-data-ownership.md) | No shared database; read models; two boundaries, two mechanisms | You're tempted by a second connection string, or a screen needs another service's data. |
| [API Gateway, Edge & Tenant Resolution](./api-gateway-edge-and-tenant-resolution.md) | The only public door; slug → tenant → membership; header projection | You're adding a client-facing route, or something about auth/tenancy looks wrong at the edge. |
| [Authentication & Identity Propagation](./authentication-and-identity-propagation.md) | Cookie at the edge, trusted headers inward, global identity | You're touching login, or where a service learns "who is calling." |
| [Integration Event Contracts](./integration-event-contracts.md) | Leaf library, past-tense records, compiler-enforced envelope | You're defining or changing an integration event. |
| [Correlation, Causation & the Audit Trail](./correlation-causation-and-audit-trail.md) | Threading identifiers; the bus-fed append-only trail | A new mutating action needs to show up in `trace-a-request`, or a trail row looks wrong. |
| [Plan Limits & Quota Replication](./plan-limits-and-quota-replication.md) | Local enforcement against a replicated quota; the overshoot window | You're adding a limit, or a tenant exceeded one. |
| [White-Label Theming](./white-label-theming.md) | CSS custom properties written at runtime; anonymous branding | You're adding a themeable surface, or branding isn't applying. |
| [Read-Through Caching](./read-through-caching.md) | Fail-open cache with tenant-scoped keys | You're caching a read, or a cached response is stale — or worse, another tenant's. |
| [Concurrency Control](./concurrency-control.md) | Conditional writes and get-or-create races | Two requests can race on one row and you need the write to be the guard. |
| [Aspire Orchestration](./aspire-orchestration.md) | Declarative resources, discovery, local emulators | You're adding a resource or wondering how a service finds another. |
| [Exception Handling & Error Shape](./exception-handling-and-error-shape.md) | One handler, one error shape, and what must never leak | You're throwing a new failure and need to know how it reaches the client. |
| [Frontend ↔ Gateway Integration](./frontend-gateway-integration.md) | Zoneless signals, one interceptor owning the slug, typed services | You're adding an Angular service or component that talks to the API. |

## Which pattern do I need?

- **"My consumer runs but nothing happens."** → [Tenant Scope on the Bus](./tenant-scope-on-the-bus.md). This is the single most likely cause, and it looks like a query bug.
- **"A user says they can see another company's data."** → [Tenant Isolation](./tenant-isolation-defense-in-depth.md), then run `@tenant-isolation-auditor`. Stop other work.
- **"My event never arrived."** → [Transactional Outbox & Inbox](./transactional-outbox-and-inbox.md) — check the outbox row committed, then that the dispatcher relayed it.
- **"A tenant exceeded their plan limit."** → [Plan Limits & Quota Replication](./plan-limits-and-quota-replication.md) — probably the documented overshoot window, not a bug.
- **"I need another service's data for this screen."** → [Database-per-Service & Data Ownership](./database-per-service-and-data-ownership.md).
- **"Who is the caller, and can I trust the id in the body?"** → [Authentication & Identity Propagation](./authentication-and-identity-propagation.md). (No, you cannot.)
- **"Support needs to know what happened."** → [Correlation, Causation & the Audit Trail](./correlation-causation-and-audit-trail.md), then the [`trace-a-request`](../../../.claude/skills/trace-a-request/SKILL.md) skill.
