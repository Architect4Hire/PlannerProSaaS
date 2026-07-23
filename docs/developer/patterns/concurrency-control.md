# Concurrency Control

**Decided by:** pattern-level; interacts with [ADR-0008](../../adr/0008-shared-schema-tenantid-discriminator.md) and [ADR-0017](../../adr/0017-replicated-quota-local-limit-enforcement.md)

---

## The shape of the problem

Read-then-write is a race. Two requests both read "no goal exists for this sprint and project," both create one, and the unique index decides the winner — after both have done work.

PlannerPro has three recurring instances:

| Race | Where | Guard |
| --- | --- | --- |
| Get-or-create a `SprintGoal` | Planning — goals are created lazily | Unique `(TenantId, SprintId, ProjectId)` + catch the violation |
| Status transition on a goal | Planning | Conditional write, not read-then-write |
| Create at a plan limit | Portfolio, Access, Files | See below — this one is **not** fully guardable |

## Let the write be the guard

The reliable pattern is to make the database decide, then handle losing:

```
try insert
  ↓ unique violation?
    ↓ yes → re-read the existing row and continue (the other request won)
    ↓ no  → we won
```

For state transitions, express the precondition **in the update** — `WHERE Status = @expected` — and treat "zero rows affected" as "somebody else moved it first." A read followed by an unconditional write is a race with extra steps.

## Uniqueness constraints are tenant-scoped

`(TenantId, SprintId, ProjectId)`, never `(SprintId, ProjectId)`. A globally unique index doesn't just over-constrain — it leaks the existence of another tenant's row through the constraint violation ([Tenant Isolation](./tenant-isolation-defense-in-depth.md)).

Which means: **the same violation that guards your race is also a tenant-scoped signal.** Both properties come from the same index.

## The race you cannot close locally

Plan-limit enforcement reads a **local quota snapshot** (ADR-0017), so two concurrent creates near the boundary can both pass. No local guard fixes this, because the authority lives in another service and the whole point is not to call it synchronously.

This is the documented overshoot window, not a bug to fix with a lock. See [Plan Limits & Quota Replication](./plan-limits-and-quota-replication.md).

## Consumers and concurrency

The inbox check and the side effect share a transaction, which makes redelivery safe (ADR-0004). But **two different consumers reacting to the same event may still race with each other** on data they both touch. Idempotency is per-consumer; it is not mutual exclusion between consumers.

## Standing rules

- Make the write the guard; don't trust a prior read.
- Express preconditions in the `WHERE` clause and check rows-affected.
- Every uniqueness constraint includes `TenantId`.
- Catch the unique violation and continue — a get-or-create that throws on a race is incomplete.
- Don't reach for a distributed lock to close the quota overshoot window; that window is a decision.
