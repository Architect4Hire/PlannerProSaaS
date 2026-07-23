# ADR-0004: Idempotent inbox over at-least-once delivery

- **Status:** Accepted
- **Date:** 2026-07-23
- **Deciders:** Robert Felkins
- **Related:** ADR-0002 (events), ADR-0003 (outbox), ADR-0009 (tenant scope on the bus), ADR-0017 (quota counters)

## Context

The outbox relay (ADR-0003) and Service Bus itself both deliver **at least once**. A consumer will eventually see the same message twice. In this system that is not a theoretical concern: a double-delivered `ProjectCreated` would double-count against `MaxProjects` (ADR-0017), a double-delivered `AttachmentUploaded` would double-charge storage, and a double-delivered event would write two audit rows for one action.

Exactly-once delivery is not available. Exactly-once *effect* is, if consumers are idempotent.

## Decision

**Every consumer dedupes on message id via an `InboxMessages` table in its own database, in the same transaction as its side effect.**

- A consumer checks `InboxMessages` for the message id; if present, it no-ops. Otherwise it applies the change **and** records the id, in one transaction.
- **Inbox rows carry `TenantId`** like any other row.
- A handler that isn't safe to run twice is treated as a bug, not an edge case.

## Consequences

**Positive**
- Redelivery is harmless, which makes the whole at-least-once chain safe.
- Counters, audit rows, and read models stay correct under retry.
- The dedupe check and the side effect share a transaction, so there is no window where one lands without the other.

**Negative**
- Every consumer pays a read and a write it would not otherwise need.
- `InboxMessages` grows without pruning — the same housekeeping debt as the outbox.
- Idempotency is a discipline, not a guarantee: a consumer author can still write a handler that dedupes correctly and *also* calls a non-idempotent external effect. Tests are the only real defense (see `test-gap-analyzer`).

**Neutral**
- Message id (the outbox row id) is the dedupe key, which makes the outbox and inbox two halves of one design.

## Alternatives considered

- **Natural idempotency only** (design every handler so repeating is harmless). Elegant where achievable, but it fails for counters and audit appends, which are inherently accumulative. Rejected as insufficient on its own — though still preferred *in addition* where it costs nothing.
- **Broker-level exactly-once.** Not available with the guarantees claimed; rejected as a foundation.
- **A distributed dedupe cache (Redis).** Faster, but introduces a second consistency domain and loses the "same transaction as the side effect" property that makes this correct.
