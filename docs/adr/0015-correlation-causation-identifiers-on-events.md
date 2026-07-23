# ADR-0015: Correlation and causation identifiers on integration events

- **Status:** Accepted
- **Date:** 2026-07-23
- **Deciders:** Robert Felkins
- **Related:** ADR-0011 (gateway projection), ADR-0013 (contracts envelope), ADR-0016 (audit trail)

## Context

One user action fans out across services: provisioning a tenant creates a default client, a sample project, a sprint calendar, a welcome notification, and audit rows in between. When support is asked "what happened when Acme signed up?", the pieces must be stitchable back into one story — after the fact, across process boundaries, from a durable record.

OpenTelemetry answers "how slow was this" with ephemeral, engineering-facing spans. It does not answer "what happened, who did it, and what caused what" months later.

## Decision

**Every integration event carries `CorrelationId`, `CausationId`, and an actor, alongside `TenantId` (ADR-0013).**

- **`CorrelationId`** is minted at the gateway when a request arrives without one, stripped if client-supplied, forwarded inward, echoed on the response, and held **constant across a request's entire fan-out**.
- **`CausationId`** is the `Id` of the event or command that *directly* caused this one — parent to child, forming a causal tree rather than a flat list.
- **Actor** is the identity projected by the edge (ADR-0011), **never** a body-supplied id.
- The business layer stamps all three when it builds the event; the outbox and dispatcher path is otherwise unchanged.

## Consequences

**Positive**
- A flat sequence of rows becomes "A caused B caused C" — which is what a support question actually needs.
- Attribution is trustworthy because the actor comes from the edge, not from the caller.
- Correlation echoed on the response gives support a handle a user can read off the screen.

**Negative**
- Discipline dependent: a publisher that forgets to propagate `CorrelationId` silently splits one story into two, and nothing fails loudly when it happens.
- `CausationId` is only as useful as the care taken setting it; a lazy implementation that always uses the root produces a list, not a tree.
- Three more fields on every event, forever.

**Neutral**
- These identifiers are for support history, deliberately separate from OpenTelemetry traces, which remain in place for engineering telemetry. Two mechanisms, two audiences.

## Alternatives considered

- **Reuse the OpenTelemetry trace id as the correlation id.** Tempting and nearly free. Rejected: trace backends are ephemeral by default and sampled, so the identifier would exist without the history behind it.
- **Correlation only, no causation.** Cheaper, and answers "what happened in this request." Rejected because "what caused this notification" is a routine support question and a flat list can't answer it.
- **Reconstruct causality from timestamps.** Rejected: unreliable under concurrency and clock skew, which is precisely when it would be needed.
