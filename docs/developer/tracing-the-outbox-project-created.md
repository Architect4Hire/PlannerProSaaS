# Tracing the Outbox: `ProjectCreated`

*One event, every layer — from the business method that builds it to the three services that react.*

> ⚠️ **This walks the designed path, not observed code.** `src/` does not exist yet. Once Prompts 6–10 have run, revise this to cite real files, and delete this note. If built behaviour differs, the code is right.

**Related:** [Transactional Outbox & Inbox](./patterns/transactional-outbox-and-inbox.md) · [Integration Event Contracts](./patterns/integration-event-contracts.md) · [Plan Limits](./patterns/plan-limits-and-quota-replication.md)

---

## Why this event

`ProjectCreated` is the busiest event in the system. It is consumed by **three** services for three different reasons, which makes it the best illustration of why publishers don't know their consumers:

| Consumer | Why it cares |
| --- | --- |
| **Planning** | Maintains a `ProjectReference` read model so the board can render project names without calling Portfolio |
| **Roadmap** | Same, for the program view |
| **Billing** | Increments the project counter for `MaxProjects` |
| **Audit** | Records it, like everything else |

Portfolio knows about none of them.

## Publish side

```
POST /api/t/acme/projects
   │
   ▼
Gateway ── resolves acme → tenant, checks membership, projects headers
   │
   ▼
Portfolio · ProjectsController ── binds CreateProjectViewModel (NO TenantId on it)
   │                              RequireTenantRole(Admin)
   ▼
Portfolio · ProjectFacade ────── validates; reads the LOCAL quota snapshot
   │                              ├─ at MaxProjects? → 402 { limit: "MaxProjects" }, stop
   │                              └─ under limit → continue
   ▼
Portfolio · ProjectBusiness ──── VM → Project entity; applies rules;
   │                              BUILDS ProjectCreated:
   │                                Id           = new Guid
   │                                TenantId     = ambient (from the gateway header)
   │                                CorrelationId= ambient (minted at the edge)
   │                                CausationId  = the originating request
   │                                Actor        = ambient — NEVER from the payload
   │                                ProjectId, Name, ColorHex, ClientId
   ▼
Portfolio · ProjectDataLayer ─── ExecuteInTransactionAsync:
   │                                ├─ repository.Add(project)   ← TenantId stamped by interceptor
   │                                └─ outbox.Enqueue(ProjectCreated)
   ▼
Commit ── 201 Created with the ServiceModel
```

The entity's `TenantId` is never set by this code. The `TenantSaveChangesInterceptor` stamps it from the ambient context — which is why a ViewModel carrying one would be both redundant and a security bug.

## Relay

```
OutboxDispatcher (Portfolio, BackgroundService)
   ├─ poll unprocessed rows, oldest first
   ├─ ServiceBusMessage:
   │     MessageId = outbox row Id        ← the inbox dedupe key
   │     Subject   = "ProjectCreated"     ← how the processor host routes it
   │     ApplicationProperties: TenantId, CorrelationId, CausationId
   │     Body      = serialized event
   └─ stamp ProcessedOnUtc
```

If the process dies between send and stamp, the row resends with the **same `MessageId`** — which is exactly what makes the inbox necessary rather than defensive.

## Consume side (×4, independently)

Each follows the identical shape:

```
ServiceBusProcessorHost
   ├─ read TenantId from application properties
   ├─ open a DI scope
   ├─ populate ITenantContext FROM THE ENVELOPE      ← the critical step
   └─ resolve IIntegrationEventConsumer<ProjectCreated>, invoke
        ↓ inside ONE transaction:
        ├─ InboxMessages contains this MessageId?
        │    ├─ yes → no-op
        │    └─ no  → apply the effect AND record the MessageId
        └─ commit
```

- **Planning** upserts a `ProjectReference` (id, name, colour, client, active).
- **Roadmap** does the same for its view.
- **Billing** increments the tenant's project counter, and publishes `TenantQuotaChanged` through **its own** outbox if the change is material.
- **Audit** appends one immutable row with the full event as `jsonb`.

Each writes **only its own database**. None calls back into Portfolio.

## The quota loop closing

Billing's `TenantQuotaChanged` travels back to Portfolio, which updates its local snapshot. That loop is what makes the next create's limit check local — and the gap between "project created" and "snapshot updated" is the documented [overshoot window](./patterns/plan-limits-and-quota-replication.md).

## Debugging checklist

1. Did the outbox row commit? No row means the transaction rolled back — the project doesn't exist either.
2. Is `ProcessedOnUtc` stamped? Unstamped means the send failed or the dispatcher isn't running.
3. Did the message reach each subscription? Check dead-letter — a missing `TenantId` lands there by design.
4. Did a consumer run and no-op? An inbox row with that `MessageId` means it already applied.
5. Did a consumer run and find nothing? **That's tenancy scope, not messaging** → [Tenant Scope on the Bus](./patterns/tenant-scope-on-the-bus.md).
