# Transactional Outbox & Inbox

**Decided by:** [ADR-0003](../../adr/0003-hand-rolled-transactional-outbox.md), [ADR-0004](../../adr/0004-idempotent-inbox-at-least-once-delivery.md)
**Rules:** [`.claude/rules/messaging.md`](../../../.claude/rules/messaging.md)

---

## The problem

Commit the domain change, then publish the event, and there are two failure windows: die after the commit and the event is lost; die after the publish and the event is a lie. Neither is acceptable when the event provisions a tenant, counts against a quota, or writes an audit row.

## The outbox (publish side)

```
Business builds the event (full envelope: Id, TenantId, CorrelationId, CausationId, actor)
    ↓
Data layer, inside ExecuteInTransactionAsync:
    ├─ write the domain change
    └─ IOutbox.Enqueue(event) → row in this service's OutboxMessages
    ↓  ONE transaction — both or neither
Commit
    ↓  asynchronously, later
OutboxDispatcher (BackgroundService)
    ├─ poll unprocessed rows, oldest first
    ├─ send as ServiceBusMessage: MessageId = row Id, Subject = event type,
    │  TenantId/CorrelationId/CausationId promoted to application properties
    └─ stamp ProcessedOnUtc
```

**The dispatcher is the only thing that sends.** Business and data code never publish inline. `OutboxMessages` rows carry `TenantId` like any other row.

A crash between send and stamp resends the same `MessageId` — which is exactly why delivery is at-least-once, and why the inbox exists.

## The inbox (consume side)

```
ServiceBusProcessorHost
    ├─ establish ITenantContext from the envelope   ← see tenant-scope-on-the-bus.md
    └─ resolve and invoke IIntegrationEventConsumer<TEvent>
        ↓  inside ONE transaction:
        ├─ has this MessageId been handled? (InboxMessages)
        │   ├─ yes → no-op, complete the message
        │   └─ no  → apply the side effect AND record the MessageId
        └─ commit
```

The dedupe check and the side effect **share a transaction**, so there is no window where one lands without the other. `InboxMessages` rows carry `TenantId` too.

## Why the effects here must be idempotent

This is not theoretical in PlannerPro:

- A double-delivered `ProjectCreated` double-counts against `MaxProjects`.
- A double-delivered `AttachmentUploaded` double-charges storage.
- A double-delivered anything writes two audit rows for one action.

Counters and appends are inherently accumulative, so natural idempotency isn't available. The inbox is the mechanism.

## Debugging: "my event never arrived"

Work outward in this order:

1. **Did the outbox row commit?** If not, the transaction rolled back — the domain change didn't happen either.
2. **Did the dispatcher relay it?** Check `ProcessedOnUtc`. Unstamped means the send failed or the dispatcher isn't running.
3. **Did the message reach the subscription?** Check the dashboard, and check dead-letter — a missing `TenantId` sends it there by design.
4. **Did the consumer run and no-op?** An inbox row with that `MessageId` means it was already handled.
5. **Did the consumer run and find nothing?** That's a tenancy scope problem, not a messaging one → [Tenant Scope on the Bus](./tenant-scope-on-the-bus.md).

## Known debt

`OutboxMessages`, `InboxMessages`, and `auditdb` all grow without bound. No pruning or rollup job is designed. Acknowledged in ADR-0003, ADR-0004, ADR-0016 and tracked in the [risk register](../../design/build-plan-and-risks.md).

## Standing rules

- Publish through the outbox, in the same transaction as the write — never inline.
- Only the dispatcher sends.
- Every consumer dedupes via the inbox in the same transaction as its side effect.
- A handler that isn't safe to run twice is a bug, not an edge case.
- A consumer writes only its own service's database.
