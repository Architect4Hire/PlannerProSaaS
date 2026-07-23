---
paths:
  - src/PlannerPro.Contracts/**
  - src/PlannerPro.Gateway/**
  - src/PlannerPro.Audit/**
  - src/PlannerPro.Audit.Core/**
  - src/PlannerPro.*/Consumers/**
  - src/PlannerPro.*.Core/Business/**
---
# Audit rules — support audit trail (tenant, correlation, causation, actor)

PlannerPro keeps a **support audit trail**: a durable, queryable record of what happened to a request
or entity over its lifecycle, so support can reconstruct any request cradle-to-grave from one place.
It is a **read model fed by the bus**, threaded by identifiers on the event contract — deliberately
**not** OpenTelemetry tracing. The store is `auditdb`, owned by `PlannerPro.Audit`.

- **Every request carries a `CorrelationId`, minted at the edge.** The gateway generates one when a
  request arrives without it, **strips any client-supplied copy**, forwards it inward, and echoes it
  on the response. Never trust a client's correlation id past the gateway.
- **Every event carries tenant, correlation, causation, and actor.** `TenantId` scopes it;
  `CorrelationId` stays constant across a request's whole fan-out; `CausationId` is the `Id` of the
  event that *directly* caused this one (a causal tree); actor is the identity projected by the edge,
  **never** a body-supplied id. Business stamps these when it builds the event.
- **`PlannerPro.Audit` is the only writer of `auditdb`, and it only appends.** One **immutable** row
  per business event — event type, tenant, correlation, causation, actor, entity ids, occurred-at, and
  the full event as a `jsonb` payload. No updates, no deletes; a correction is a new row.
- **Audit rows are tenant-scoped like everything else.** A support query for tenant A must never
  surface tenant B's rows, and the platform-admin surface is the only place that reads across tenants
  — under an explicit bypass context.
- **Audit consumption is idempotent, via the inbox.** Dedupe on the event `Id` in the same transaction
  as the append. Redelivery must never double-write.
- **Audit is a read model, not a source of truth.** It never calls back into a service and never
  drives domain behavior; it only records. The owning service stays authoritative for its data.
- **Cradle to grave means every mutating action is audited.** A state-changing action with no audited
  event is a gap — actions that don't already publish (login, settings changed, plan changed,
  membership removed) get an audit-worthy event. Shipping a new mutating endpoint includes emitting
  and auditing it (use the `add-audit-event` skill).
- **Keep secrets and needless PII out of the trail.** The `jsonb` records the event a consumer already
  sees — no tokens, passwords, invitation tokens, or fields support doesn't need. The trail is durable
  and queryable; treat it as disclosable.
- **The support-query surface is read-only and gateway-fronted.** Queries by correlation id / entity /
  actor / time go through one auth-protected gateway route to the Audit service; never expose
  `auditdb` directly. The repeatable workflow is the `trace-a-request` skill.
