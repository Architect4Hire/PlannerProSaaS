---
name: add-audit-event
description: >
  Make a mutating action show up in PlannerPro's support audit trail. Use when shipping a new
  state-changing endpoint, or when a trace comes up short and the missing step turns out never to have
  been audited — e.g. "membership removal isn't in the trail", "we can't see who changed the plan".
  Adds or extends the integration event, ensures the envelope is complete, and confirms the Audit
  service records it.
---

# Add an audit event

The trail's promise is **cradle to grave**: any mutating action should be reconstructable later. A
state-changing action that publishes nothing is a **coverage gap** — and gaps are found at exactly the
wrong moment, during a support investigation.

Read [`.claude/rules/audit.md`](../../rules/audit.md) first.

## When this applies

Any action that changes state. The obvious ones already publish (a goal set, a task changed, a project
created). The ones that get missed are the administrative and identity actions: login and failed
login, settings changed, branding changed, plan changed, membership role changed, member removed,
invitation sent or revoked, tenant suspended. If it would matter in a support conversation, it's
audit-worthy.

## Steps

1. **Check whether an event already exists.** Often the action publishes something and the trail is
   fine — the gap is that Audit doesn't consume it, or the envelope is incomplete. Don't add a second
   event for the same fact.
2. **Add or extend the record in `PlannerPro.Contracts`** — immutable, past-tense, carrying only what
   a consumer needs. Changing an existing event is a contract change affecting every consumer; list
   them and update them together.
3. **Complete the envelope.** `Id`, **`TenantId`**, `CorrelationId`, `CausationId`, actor. Business
   stamps these when it builds the event: `CorrelationId` carries through from the request,
   `CausationId` is the `Id` of the event or command that directly caused this one, actor is the
   identity projected by the edge — **never** a body-supplied id.
4. **Publish through the outbox** in the same transaction as the domain write. Nothing about the
   audit path changes this; the trail is a consumer like any other.
5. **Confirm Audit consumes it** and appends one immutable row — event type, tenant, correlation,
   causation, actor, entity ids, occurred-at, and the full event as `jsonb`. Idempotent via the inbox.
6. **Check what you're recording.** The `jsonb` holds what a consumer already sees, so no tokens, no
   passwords, no invitation tokens, no PII support doesn't need. The trail is durable, queryable and
   should be treated as disclosable.
7. **Verify by tracing.** Perform the action, then use the `trace-a-request` skill to pull it back by
   correlation id and confirm it reads as a sensible step in the story — right actor, right causal
   parent, right entity.

## Checklist before done
- [ ] No duplicate event for a fact that already publishes
- [ ] Past-tense immutable record in `Contracts`; consumers of any changed event updated
- [ ] Envelope complete: `Id`, `TenantId`, `CorrelationId`, `CausationId`, actor — actor from the edge
- [ ] Published through the outbox, same transaction as the write
- [ ] Audit appends one immutable row, idempotently; no update, no delete
- [ ] No secrets or needless PII in the payload
- [ ] Traced back by correlation id and reads correctly in the timeline
