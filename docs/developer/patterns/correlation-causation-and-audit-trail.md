# Correlation, Causation & the Audit Trail

**Decided by:** [ADR-0015](../../adr/0015-correlation-causation-identifiers-on-events.md), [ADR-0016](../../adr/0016-audit-bounded-context-bus-fed-support-trail.md)
**Rules:** [`.claude/rules/audit.md`](../../../.claude/rules/audit.md) · **Skills:** `add-audit-event`, `trace-a-request`

---

## Two different questions

| Question | Answered by | Lifetime |
| --- | --- | --- |
| "How slow was this?" | OpenTelemetry traces, Aspire dashboard | Ephemeral, sampled |
| "What happened, and who did it?" | The audit trail | Durable, complete |

They are deliberately separate mechanisms for separate audiences. Reusing the trace id as a correlation id was considered and rejected — trace backends are sampled and ephemeral, so the identifier would outlive the history behind it.

## The three identifiers

- **`CorrelationId`** — minted at the gateway when absent, **stripped if client-supplied**, forwarded inward, echoed on the response, and held constant across the entire fan-out of one request.
- **`CausationId`** — the `Id` of the event or command that *directly* caused this one. Parent to child. This is what turns a flat list into a causal tree.
- **Actor** — the identity projected by the edge (ADR-0011). **Never** a body-supplied id.

Business stamps all three when it builds the event. The outbox and dispatcher path is otherwise unchanged.

## What a trail row holds

One **immutable** row per business event in `auditdb`: event type, **tenant**, correlation, causation, actor, entity ids, occurred-at, and the full event as `jsonb`. Append-only — never updated, never deleted; a correction is a new row. Idempotent via the inbox.

**Audit rows are tenant-scoped like everything else.** A support query for one tenant must never surface another's; cross-tenant reads happen only on the platform surface under an explicit bypass context.

## Reading a trail

Most investigations funnel: start broad (actor or time window), find the `CorrelationId` that matters, pull that request's full trail, then read the `CausationId` tree:

```
TenantProvisioned            (root — caused by the signup request)
├── ClientCreated            causation = TenantProvisioned.Id
│   └── ProjectCreated       causation = ClientCreated.Id
└── NotificationSent         causation = TenantProvisioned.Id
```

That structure is what makes "why did this person get two emails?" answerable. A lazy implementation that always sets `CausationId` to the root produces a list, not a tree, and answers nothing.

## Coverage is a discipline

The trail is only cradle-to-grave if **every mutating action publishes something**. The actions that get missed are the administrative ones: login, settings changed, plan changed, membership removed, invitation revoked. These don't naturally publish, so they need an audit-worthy event added deliberately — that's what the `add-audit-event` skill is for.

Gaps are found during an investigation, which is the worst possible moment.

## What must not be in the payload

The `jsonb` records what a consumer already sees. **No tokens, no passwords, no invitation tokens, no PII support doesn't need.** The trail is durable and queryable; treat it as disclosable.

## Standing rules

- `CorrelationId` minted at the edge, stripped if client-supplied, constant across the fan-out.
- `CausationId` is the direct parent, not the root.
- Actor comes from the edge, never the body.
- Audit appends only; never updates, never deletes, never calls back into a service.
- A mutating action with no audited event is a coverage gap — fix it, don't route around it.
