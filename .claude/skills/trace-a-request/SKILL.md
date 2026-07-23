---
name: trace-a-request
description: >
  Reconstruct what happened to a PlannerPro request or entity, cradle-to-grave, from the support audit
  trail. Use for support and investigation questions — e.g. "what happened to this sprint goal", "why
  did this member get two invites", "show everything this user did today", "trace correlation id Z end
  to end". Queries auditdb through the gateway by correlation id, entity, actor or time window and
  returns an ordered, causal timeline. Read-only — it reads the trail, it never changes anything.
---

# Trace a request

The support audit trail is a durable record every business event lands in, threaded by the identifiers
on the event envelope. This skill uses it to answer "what happened to X" **without** touching any
service's own database. It is **read-only**: it reconstructs, it never repairs.

Read [`.claude/rules/audit.md`](../../rules/audit.md) first. Two rules shape everything here: **query
through the gateway's audit route, never `auditdb` directly**, and **the trail is tenant-scoped** — a
support query for one tenant must never surface another's rows.

## Pick the entry point

| You have | Query by | Gets you |
|---|---|---|
| A request/trace id | **`CorrelationId`** | the *whole fan-out* of one originating request across every service |
| A goal / task / project / membership | **entity id** | that entity's *entire life* |
| A user | **actor** | everything *that person did* (optionally within a window) |
| "Around 14:00 yesterday" | **time window** | everything in a span, to narrow to a correlation |

Most investigations **funnel**: start broad (actor or time), find the `CorrelationId` that matters,
then pull that request's full trail and read its causal structure.

## Steps

1. **Choose the axis** and hit the **gateway audit route** (auth required) with the matching filter,
   scoped to the tenant in question. Never query `auditdb` directly, and never join across a service's
   own tables.

2. **Order by occurred-at** to get the timeline, then **read the causal tree** via `CausationId`: the
   row whose `CausationId` is the originating request is the root; every other row hangs off its parent
   (`CausationId` → parent's event `Id`). That turns a flat list into "A caused B caused C" — e.g.
   *tenant provisioned* → `TenantProvisioned` → default client created → sample project created → a
   welcome notification recorded.

3. **Read the actor and payload** on each row: *who* did it (the identity projected at the edge) and
   the detail in the `jsonb`. Only support-facing fields are there by design — secrets and needless PII
   were kept out at write time. If something you need isn't recorded, that's a **coverage gap**, not a
   query to work around.

4. **Report the story, not the rows.** An ordered, attributed timeline: at T, actor did action on
   entity, which caused …, ending in …. Name the `CorrelationId` you traced so it can be re-queried.

## When the trail comes up short

- **A step you expected is missing** → the action likely isn't audited. That's a coverage gap; fix it
  with the [`add-audit-event`](../add-audit-event/SKILL.md) skill rather than querying the owning
  service's database.
- **Two traces that should be one** → a `CorrelationId` wasn't propagated across a hop. Check the
  gateway mint/forward and the publish-site stamping.
- **A step attributed to the wrong tenant, or visible from the wrong tenant** → stop. That's an
  isolation finding, not a tracing problem. Report it and run `@tenant-isolation-auditor`.
- **You need live latency or spans, not history** → that's engineering telemetry (the Aspire
  dashboard), a different use case. The trail answers "what happened", not "how slow".

## Checklist before trusting the answer
- [ ] Queried the **gateway audit route**, not `auditdb` or any service's database
- [ ] Scoped to one tenant, and nothing from another tenant appeared
- [ ] Anchored on a single **`CorrelationId`** (or a clearly stated entity/actor/time filter)
- [ ] Ordered by time **and** reconstructed the **`CausationId`** tree — not just a flat list
- [ ] Each step attributed to an actor; the traced `CorrelationId` cited
- [ ] Any missing step flagged as a coverage gap to fix via `add-audit-event`, not routed around
