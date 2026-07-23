# ADR-0005: Thin host + `.Core` layered library; one-way acyclic references

- **Status:** Accepted
- **Date:** 2026-07-23
- **Deciders:** Robert Felkins
- **Related:** ADR-0001 (service decomposition), ADR-0013 (contracts as leaf), `docs/developer/patterns/layered-service-architecture.md`, `.claude/rules/backend.md`

## Context

Eight services built by an agent will drift unless their internal shape is fixed and mechanically checkable. Two failure modes matter: logic accumulating in controllers and `Program.cs` (making a service untestable and its entry points load-bearing), and services quietly referencing each other's projects (making the boundary decorative).

A deliberate choice was also made *not* to adopt tactical DDD (aggregate roots, value objects, domain events) for this repo, in favour of the same layered structure used across the showcase portfolio — consistency between reference applications was judged more valuable here than tactical-DDD depth.

## Decision

**Every service is two projects: a thin host and a `.Core` class library holding a facade → business → data layer → repository stack.**

- **Host** (`PlannerPro.<Service>`) — `Controllers/`, `Consumers/`, and `Program.cs` as composition root. Nothing else.
- **`.Core`** (`PlannerPro.<Service>.Core`) — the whole stack plus models, validators, mappers. EF Core lives here.
- **Strict layer responsibilities:** controller/consumer binds and delegates; facade validates and caches; business maps and applies rules and *builds* events; data layer composes operations and enqueues the outbox row in one transaction; repository does EF queries only.
- **Three model types at the boundary:** ViewModels in, ServiceModels out, Domain entities internal. No EF entity crosses the controller; no Domain entity crosses a *service* boundary.
- **References are one-way and acyclic:** `Contracts` ← `Shared` ← `<Service>.Core` ← `<Service>` ← `AppHost`. A host references *its own* `.Core` and no other service's anything.
- Registration is owned by `.Core` via `Add<Service>Core()`.

## Consequences

**Positive**
- Every service looks the same, so a reviewer (or an agent) knows where to look and where a piece of logic belongs.
- The stack is testable without a host; the host is trivial enough to need little testing.
- A sideways or backwards project reference is a compile-time failure, not a code-review opinion.

**Negative**
- More projects and more ceremony than the work sometimes warrants — a two-field lookup still travels through four layers.
- Layer discipline is prescriptive; it will occasionally feel like friction on a simple change.
- **Not tactical DDD.** Aggregates and invariant enforcement are conventions here, not types. A domain with genuinely complex invariants would outgrow this shape, and that would be the trigger to revisit.

**Neutral**
- `ViewModel` never carries `TenantId` (ADR-0008); tenant identity is ambient, which slightly reduces what the boundary types must express.

## Alternatives considered

- **Tactical DDD (aggregate roots, value objects, domain events → integration events).** A better fit for a domain with complex invariants, and a stronger showcase of modelling depth. Rejected for portfolio consistency: JobBoard uses this layering, and two reference apps that disagree teach less than two that agree. Revisit if PlannerPro's rules outgrow the layering.
- **Vertical slices / MediatR handlers.** Less ceremony per feature, and a fine choice. Rejected because the layer boundaries are what make an agent's output predictable across eight services.
- **Single project per service.** Rejected: the host becomes load-bearing and the stack becomes untestable in isolation.
