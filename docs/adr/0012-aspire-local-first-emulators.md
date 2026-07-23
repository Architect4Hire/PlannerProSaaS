# ADR-0012: Aspire local-first topology with emulators

- **Status:** Accepted
- **Date:** 2026-07-23
- **Deciders:** Robert Felkins
- **Related:** ADR-0001 (database-per-service), ADR-0002 (Service Bus), `.claude/rules/aspire.md`

## Context

The system needs eight databases, a message broker, blob storage, a cache, a gateway, eight service hosts, and an Angular dev server. A contributor — or an agent — must be able to run all of it with one command and no cloud account, or the feedback loop dies and the repo stops being a usable showcase.

## Decision

**Every resource is declared in `PlannerPro.AppHost` and runs as a local container; `aspire run` starts the entire system.**

- Postgres server with a database per service; Azure Service Bus **emulator**; Azurite for blobs; Redis for caching.
- Services are wired with `WithReference(...)` and ordered with `WaitFor(...)`. **No connection strings, broker addresses, blob endpoints, or `localhost:port` anywhere in code.**
- An **emulator-backed** Azure resource is in bounds because it is a local container. A resource requiring a real subscription (`AsExisting`, real provisioning) is not.
- Cross-cutting telemetry, health, resilience, and service discovery live once in `ServiceDefaults` — including enriching spans and log scopes with `TenantId`.

## Consequences

**Positive**
- One command runs the whole system with zero cloud spend, which is what makes an eight-service showcase practical to explore.
- The Aspire dashboard gives logs, traces, and health across every service without extra setup.
- Configuration is injected, so no environment-specific values live in source — which also means the `secret-guard` hook can treat any literal credential as a defect.

**Negative**
- The Service Bus emulator is not the real broker; behaviour under load, at scale, and around dead-lettering will differ. Anything depending on those specifics needs verification against the real service before it can be trusted.
- Container startup for eight services plus infrastructure is slow on a cold start and hungry with memory.
- **There is no deployment story yet.** Local-first is a development decision that says nothing about production, and the gap is real (see `docs/design/build-plan-and-risks.md`).

**Neutral**
- Aspire's API surface moves between versions; docs deliberately instruct verification against https://aspire.dev rather than trusting recalled API names.

## Alternatives considered

- **Docker Compose.** Portable and familiar. Rejected: no service discovery integration, no dashboard, and configuration wiring becomes manual — most of what Aspire is being demonstrated for.
- **Real Azure resources in a dev subscription.** Highest fidelity, and eventually necessary for verification. Rejected as the default: cost, credentials, and a contributor barrier that kills the showcase.
- **In-memory bus for local development.** Faster, but hides exactly the at-least-once and dead-letter behaviour ADR-0004 and ADR-0009 exist to handle.
