# ADR-0020: Suspended tenants become read-only, not locked out

- **Status:** Accepted
- **Date:** 2026-07-23
- **Deciders:** Robert Felkins
- **Related:** ADR-0006 (gateway), ADR-0011 (edge enforcement), ADR-0017 (limits), ADR-0018 (lifecycle)

## Context

A tenant's subscription can lapse: a trial ends, a payment fails, an account is cancelled. The product has to decide what those users see. The instinctive implementation is to block sign-in — it is one line and it feels decisive.

It is also the version that generates the most support load, because the first thing a locked-out customer does is contact support asking for their data, usually while annoyed.

## Decision

**Tenants in `Suspended`, `PastDue`, or `Cancelled` status become read-only: reads and export continue to work, writes are refused at the edge.**

- Enforced at the **gateway** (ADR-0011), so no service needs its own status check.
- Users can still sign in, view their boards, roadmaps and history, and export their data.
- Mutations are refused with a clear, actionable status.
- **Suspension is never implemented as an authentication failure.** A user in a suspended tenant is not a user with bad credentials, and conflating them produces a confusing and hostile experience.
- Cancelled tenants are purged after a retention window (length is an open product decision).

## Consequences

**Positive**
- A lapsed customer can see exactly what they'll lose, which is a considerably better recovery prompt than a locked door.
- Support load drops: the most common "I've been locked out, give me my data" ticket disappears.
- Read-only is the honest state — the data still exists, so pretending otherwise serves nobody.

**Negative**
- Suspended tenants continue to consume storage and query capacity, so non-payment has an ongoing cost until the retention window expires.
- Every write path must respect a status the gateway enforces, which means edge enforcement has to be complete — one unguarded mutation route undoes it.
- "Read-only" needs to be visible and comprehensible in the UI, or it reads as a bug rather than a state.

**Neutral**
- Trial expiry uses the same mechanism as payment failure, so there is one code path rather than two.

## Alternatives considered

- **Block sign-in entirely.** Simplest to implement and the most common approach. Rejected on product grounds: it maximises support load and destroys the recovery moment.
- **Delete data on cancellation.** Rejected: irreversible, hostile, and legally fraught.
- **Degrade gradually (reduce limits before suspending).** Interesting, and arguably kinder still. Rejected as more states than the product can currently explain clearly to a user.
