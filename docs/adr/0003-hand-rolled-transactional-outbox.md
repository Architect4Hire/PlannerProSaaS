# ADR-0003: Hand-rolled transactional outbox

- **Status:** Accepted
- **Date:** 2026-07-23
- **Deciders:** Robert Felkins
- **Related:** ADR-0002 (events), ADR-0004 (inbox), ADR-0008 (tenant discriminator), `docs/developer/patterns/transactional-outbox-and-inbox.md`

## Context

A service that commits a domain change and *then* publishes an event has two failure windows: the process can die after the commit and before the send (the event is lost), or after the send and before the commit (the event is a lie). Neither is acceptable for provisioning a tenant, counting a project against a quota, or recording an audit row.

The standard fix is a transactional outbox: write the event into the service's own database, in the same transaction as the domain change, and relay it afterward.

## Decision

**We will implement the transactional outbox by hand, in `PlannerPro.Shared`, rather than adopting a messaging framework.**

- The business layer *builds* the event; the data layer writes it to that service's own `OutboxMessages` table via `IOutbox`, **inside the same transaction** as the domain write, using `ExecuteInTransactionAsync`.
- A background `OutboxDispatcher` polls unprocessed rows oldest-first, sends each as a `ServiceBusMessage` with `MessageId` = the row `Id` and `Subject` = the event-type name, promotes `TenantId`/`CorrelationId`/`CausationId` to application properties, then stamps `ProcessedOnUtc`.
- **The dispatcher is the only thing that sends.** Nothing publishes inline from business or data code.
- **Outbox rows carry `TenantId`** like any other row in a tenant-scoped database (ADR-0008).

## Consequences

**Positive**
- A domain write and its event are atomic. Either both land or neither does.
- The mechanism is small, readable, and fully owned — which matters when it is also the thing that carries tenant scope onto the bus (ADR-0009).
- No framework version to track, no opinionated conventions to fight.

**Negative**
- Polling adds latency between commit and publish, and load on each database.
- A crash between send and stamp resends the same `MessageId` — hence at-least-once, hence ADR-0004.
- `OutboxMessages` grows without a pruning job. Housekeeping debt, acknowledged.
- We own the bugs. A framework would have had these paths tested by thousands of users.

**Neutral**
- The dispatcher's poll interval is a tunable trade between latency and database load.

## Alternatives considered

- **MassTransit.** Mature, well-tested, has an outbox. Rejected for this repo because the outbox is one of the mechanisms being *demonstrated* — hiding it behind a framework defeats the purpose. For a commercial delivery with no teaching goal, MassTransit is very often the right call, and this ADR should not be read as a general recommendation against it.
- **Dual write with retry.** Rejected: doesn't close the failure windows, it narrows them.
- **Change-data-capture from the transaction log.** Powerful and genuinely atomic, but heavy infrastructure for this scale and hard to run locally.
