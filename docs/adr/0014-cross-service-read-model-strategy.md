# ADR-0014: Cross-service read models over query composition

- **Status:** Accepted
- **Date:** 2026-07-23
- **Deciders:** Robert Felkins
- **Related:** ADR-0001 (database-per-service), ADR-0002 (events), ADR-0004 (inbox), ADR-0016 (audit), ADR-0017 (quotas)

## Context

The sprint board shows projects. Projects are owned by Portfolio; the board is owned by Planning. With no shared database (ADR-0001) and no synchronous calls (ADR-0002), Planning needs another way to know a project's name.

The same shape recurs across the system: Roadmap needs projects, Billing needs countable facts from four services, Audit needs everything, Notifications needs enough context to write an email.

## Decision

**A service that needs another service's data consumes that service's events and maintains a small local read model in its own database.**

- Planning keeps a `ProjectReference` (id, name, colour, client, active flag) fed by `ProjectCreated`/`ProjectArchived`. It never queries Portfolio.
- Read models hold **only the fields actually rendered or needed for a decision** — not a mirror of the source table.
- The owning service stays **authoritative**; a read model is never a source of truth and never drives a write in the owning service.
- Consumption is idempotent via the inbox (ADR-0004), so redelivery doesn't duplicate a reference row.
- Audit (ADR-0016) and Billing's counters (ADR-0017) are instances of this same pattern, not exceptions to it.

## Consequences

**Positive**
- Reads are local and fast; the board renders without a network hop to Portfolio.
- Planning stays available when Portfolio is down — the board still works with the last known project list.
- The pattern is uniform, so a new cross-service read need has an obvious answer.

**Negative**
- **Data is duplicated and eventually consistent.** A renamed project shows the old name until the event lands, and that lag is visible to users on a screen they're actively looking at.
- A missed or dead-lettered event leaves a read model silently stale, with no natural repair path. **A reconciliation mechanism is needed and does not exist yet** — this is a known gap, not a solved problem.
- Every read model is another consumer to write, test for idempotency, and keep in step with the source's event shape.

**Neutral**
- Read models are tenant-scoped like any other entity (ADR-0008), so a stale reference is at least always the right tenant's stale reference.

## Alternatives considered

- **Query composition at the gateway** (fan out, stitch responses). No duplication and always fresh, but it re-couples availability — the board would fail when Portfolio does — and adds latency to a hot read. Rejected for the board; still reasonable for rare admin screens, and this ADR does not forbid it there.
- **A shared reference database.** Rejected: ADR-0001.
- **Synchronous call with a cache.** Rejected: the cache is a read model with worse invalidation semantics and a hard dependency underneath it.
