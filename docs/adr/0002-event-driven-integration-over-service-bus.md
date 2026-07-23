# ADR-0002: Event-driven integration over Azure Service Bus

- **Status:** Accepted
- **Date:** 2026-07-23
- **Deciders:** Robert Felkins
- **Related:** ADR-0001 (database-per-service), ADR-0003 (outbox), ADR-0004 (inbox), ADR-0009 (tenant scope on the bus), ADR-0013 (contracts), `docs/design/high-level-design.md` §6

## Context

With no shared database (ADR-0001), services still need to react to each other: provisioning a tenant must create its default client; archiving a project must remove it from the board; uploading a file must count against a storage quota; every mutating action must reach the audit trail. The options are synchronous calls between services or asynchronous messages.

Synchronous calls would make each write path only as available as its slowest dependency, and would re-couple the services at runtime — a distributed monolith with added latency.

## Decision

**We will integrate services exclusively through immutable, past-tense integration events published over Azure Service Bus.**

- Events are records in `PlannerPro.Contracts` (ADR-0013), carrying `Id`, **`TenantId`**, `CorrelationId`, `CausationId`, and actor.
- A service reacts by **consuming** an event and doing work in **its own** store. It never reaches back into the publisher, and never substitutes a synchronous call.
- Service Bus runs as a **local emulator container** in development (ADR-0012).
- Delivery is **at least once**; consumers are idempotent (ADR-0004).

## Consequences

**Positive**
- Publishers don't know their consumers; adding a subscriber (Audit, Billing) requires no publisher change.
- A consumer being down delays an effect rather than failing the originating request.
- The event stream is the natural feed for the audit trail (ADR-0016) and quota accounting (ADR-0017).

**Negative**
- Everything cross-service is eventually consistent. Some product behavior must be designed around a visible lag — a newly created project appears on the board a beat later.
- Debugging spans processes; correlation identifiers (ADR-0015) become mandatory rather than nice-to-have.
- **Every event now carries tenant scope**, and a consumer has no ambient request to derive it from. That problem is significant enough to warrant its own decision (ADR-0009).

**Neutral**
- Event schema evolution becomes a contract-management discipline; changing an existing event affects every consumer.

## Alternatives considered

- **Synchronous HTTP between services.** Rejected: couples availability, adds latency to every write, and makes the audit trail a cross-cutting call rather than a subscriber.
- **A shared "integration" database table polled by consumers.** Rejected: a shared database by another name (ADR-0001).
- **RabbitMQ or Kafka.** Both reasonable. Rejected for this stack because Service Bus has a first-class Aspire integration and a local emulator, keeping the whole system runnable with no cloud spend (ADR-0012). Kafka's log-retention model would be a better fit if event replay became a requirement — noted as a future reconsideration trigger.
