# ADR-0001: Microservices with database-per-service

- **Status:** Accepted
- **Date:** 2026-07-23
- **Deciders:** Robert Felkins
- **Related:** ADR-0002 (event-driven integration), ADR-0005 (layering & references), ADR-0008 (tenant discriminator), `docs/design/high-level-design.md` §4–5, `CLAUDE.md`

## Context

PlannerPro began as a deliberately right-sized single-tenant modular application: one API, minimal endpoints, EF Core straight against one `DbContext`. That was the correct architecture for what it was. Turning it into a multi-tenant, white-label SaaS changed the forces: white-label branding, plans and limits, per-tenant sprint calendars, invitations, and a platform-operations surface are capabilities driven by different actors, changing at different rates, with materially different availability requirements — billing must not take down sprint planning.

The showcase goal reinforces this: PlannerPro exists to demonstrate a *multi-tenant* microservice architecture that stays correct and independently evolvable, not a distributed monolith. The primary risk remains **coupling through shared data** — the moment two services share a table, they can no longer deploy, reason, or scale independently.

## Decision

**We will decompose the system into eight bounded-context services, each owning its own PostgreSQL database, with no shared database — ever.**

- Each service is the sole reader and writer of its own store: `accessdb`, `portfoliodb`, `planningdb`, `roadmapdb`, `filesdb`, `billingdb`, `auditdb`, `notificationsdb` (declared in `AppHost.cs`).
- Decomposition is by **business capability**, not by entity or layer.
- A service needing another service's data **consumes that service's event and keeps a local copy** (ADR-0014), or routes a query through the gateway — never a second connection string.
- **Tenants share a database *within* a service**, separated by a `TenantId` discriminator (ADR-0008). That is the tenancy model, not an exception to this rule: the service boundary is physical, the tenant boundary is logical, and they are different concerns.
- **Adding a service is an architectural decision, not a feature.**

## Consequences

**Positive**
- True independence: each service deploys, migrates, and scales on its own.
- Service boundaries are enforced by the strongest possible mechanism — there is physically no way to reach another service's data.
- Blast radius of a schema change is contained to one service.

**Negative**
- Cross-service queries ("the board, with client names and plan limits") are not a JOIN; they require event-carried denormalization (ADR-0014).
- No cross-service ACID transaction; consistency across services is eventual (accepted via ADR-0003/0004).
- **Tenant isolation must now be solved eight times over.** In a monolith one set of query filters covers everything; here the mechanism must live once in `PlannerPro.Shared` and be inherited correctly by every service (ADR-0008). This is the single largest cost of this decision.
- Some reference data is duplicated (a project's name travels inside `ProjectCreated`). A deliberate trade against coupling.

**Neutral**
- One physical PostgreSQL *server* hosts the eight logical databases locally; a deployment convenience, not a shared schema.

## Alternatives considered

- **Stay a modular monolith with query filters** (the original `multitenancy-plan.md`). Genuinely the cheaper and lower-risk path, and the right answer for many teams — one migration set, one filter mechanism, no distributed failure modes. Rejected here because it doesn't demonstrate what this repo exists to demonstrate, and because billing, files, and notifications have availability and scaling profiles that argue for separation. **This rejection is a showcase decision as much as an engineering one, and should be read that way.**
- **Shared database, service code split.** Rejected: re-introduces the coupling the exercise avoids; a shared table is a hidden contract.
- **Schema-per-service in one database.** Cheaper isolation, but still one failure and lock domain, and an easy path back to accidental cross-schema joins.
- **Four services instead of eight.** Considered seriously. Rejected because Billing, Files, Audit and Roadmap each have a distinct lifecycle and a distinct failure tolerance; folding them in would have produced four services with four unrelated reasons to change.
