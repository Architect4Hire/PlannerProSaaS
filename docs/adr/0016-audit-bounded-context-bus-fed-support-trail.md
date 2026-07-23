# ADR-0016: The Audit bounded context — a bus-fed support trail

- **Status:** Accepted
- **Date:** 2026-07-23
- **Deciders:** Robert Felkins
- **Related:** ADR-0001 (database-per-service), ADR-0004 (inbox), ADR-0008 (tenant scope), ADR-0014 (read models), ADR-0015 (correlation/causation)

## Context

Support — human or agent — needs to reconstruct the cradle-to-grave story of a request or entity: who signed up, who invited whom, who moved a goal to `AtRisk`, what notifications that triggered — across every service, from **one** place, surviving restarts.

The system already emits that story as integration events, and ADR-0015 makes each carry the identifiers needed to stitch and attribute it. What's missing is a durable, queryable collector. Two constraints shape the answer: no shared database (ADR-0001) means a per-service audit table would force a support query to fan out across eight databases, and adding a service is a deliberate decision — so this ADR exists to propose one explicitly.

## Decision

**A dedicated, consumer-only `PlannerPro.Audit` bounded context subscribes to every business event and appends it to its own `auditdb`, exposing a read-only support-query surface through the gateway.**

- Thin host + layered `.Core` like every service (ADR-0005), with **consumers and no public mutations**. It owns `auditdb` and writes only it.
- One **immutable** row per event: event type, **tenant**, correlation, causation, actor, entity ids, occurred-at, and the full event as `jsonb`. Idempotent via the inbox (ADR-0004).
- **Append-only.** Never updated, never deleted; a correction is a new row.
- **Audit rows are tenant-scoped** (ADR-0008). A support query for one tenant must never surface another's, and cross-tenant reads happen only on the platform surface under an explicit bypass context.
- **Never a source of truth.** It never calls back into a service and never drives domain behaviour.
- One auth-protected gateway route for queries by correlation, entity, actor, or time window. `auditdb` is never exposed directly.

## Consequences

**Positive**
- One durable store answers "what happened," across services, after the fact — the support use case met from one place.
- Boundaries hold: no shared database, no service reaching into another, consumers still idempotent.
- Append-only plus `jsonb` keeps the sink simple and tolerant of new event shapes without a migration per event.

**Negative**
- A ninth thing to build, run, and operate.
- **`auditdb` grows unbounded** without retention or rollup — the same housekeeping debt as the outbox and inbox, and it grows fastest.
- **Coverage is a discipline, not a guarantee.** The trail is only cradle-to-grave if every mutating action publishes something. Actions that don't naturally publish (login, settings changed, plan changed, member removed) need an audit-worthy event. The `audit.md` rule and `add-audit-event` skill exist to enforce this, and gaps are found at the worst possible moment.
- A tenant's audit rows are part of their data for export and deletion purposes — which interacts awkwardly with append-only.

**Neutral**
- Audit subscribes to the same events other consumers receive; it adds a subscriber, not a publish path.

## Alternatives considered

- **Fold audit into Notifications.** Both consume everything, so it looks tempting. Rejected: conflates sending with recording and makes `notificationsdb` dual-purpose.
- **Per-service audit tables via a `Shared` mechanism.** Respects database-per-service most strictly, but a lifecycle query then fans out across eight databases. Rejected against "one place."
- **An OpenTelemetry backend (Jaeger/Tempo/Seq).** Purpose-built for spans, ephemeral by default, engineering-facing. Answers "how slow," not "what happened and who did it." Wrong tool.
- **Event-store-as-audit (replay the bus).** Attractive, but requires log-retention semantics the chosen broker doesn't provide (ADR-0002).
