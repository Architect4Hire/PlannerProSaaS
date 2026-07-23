---
paths:
  - src/PlannerPro.Shared/**
  - src/PlannerPro.Contracts/**
  - src/PlannerPro.*/Consumers/**
---
# Messaging rules — Azure Service Bus + hand-rolled outbox/inbox

Services talk to each other only through **integration events** over **Azure Service Bus** (a local
emulator in dev). Reliability is a **hand-rolled transactional outbox** — no MassTransit, no
third-party outbox. The mechanism lives once in `PlannerPro.Shared`; the event records live in
`PlannerPro.Contracts`; consumers live in each service host.

- **Events are facts, in `Contracts`.** An integration event is an immutable `record` implementing
  `IIntegrationEvent`, named in the **past tense** (`ProjectCreated`, `InvitationAccepted`), carrying
  only the fields a consumer needs — IDs plus the minimum denormalized data to avoid a call-back. No
  behavior, no EF, no service's Domain types. Every event carries `Id`, **`TenantId`**,
  `CorrelationId`, `CausationId`, and the acting identity. Changing an existing event is a **contract
  change** affecting every consumer; treat it as such.
- **Publish through the outbox, atomically.** Business *builds* the event; the data layer writes it
  to the service's own `OutboxMessages` table via `IOutbox` **inside the same transaction** as the
  domain write (`ExecuteInTransactionAsync`). A write that commits without its outbox row — or an
  event sent outside the outbox — is the bug this rule exists to prevent.
- **The dispatcher is the only sender.** `OutboxDispatcher` (in `Shared`, a `BackgroundService`)
  polls unprocessed rows oldest-first, sends each as a `ServiceBusMessage` with `MessageId` = the row
  `Id` and `Subject` = the event-type name, promotes `TenantId`/`CorrelationId`/`CausationId` to
  application properties, then stamps `ProcessedOnUtc`. Delivery is **at-least-once** (a crash between
  send and stamp resends the same `MessageId`).
- **Tenant scope comes from the envelope, established centrally.** A consumer runs on a background
  thread with no HTTP request. The `ServiceBusProcessorHost` opens a scope and populates
  `ITenantContext` from the event's `TenantId` **before** invoking the consumer. A consumer must never
  resolve its own tenancy, and a message arriving without a `TenantId` is dead-lettered rather than
  processed under a guessed scope. This is the most breakable mechanism in the system — treat changes
  to it as high-risk.
- **Consumers are idempotent, via the inbox.** A `<Event>Consumer` implements
  `IIntegrationEventConsumer<TEvent>` (from `Shared`). In the same transaction as its side effect it
  checks `InboxMessages` for the message ID, applies the change and records the ID, or no-ops on a
  repeat. A handler that isn't safe to run twice is a bug, not an edge case.
- **A consumer writes only its own service's database.** Reacting to another service's event means
  doing work in *your* store — never reach back into the publisher, and never add a synchronous call
  in place of the event.
- **Read models are local copies, kept deliberately small.** Planning keeps a `ProjectReference` fed
  by `ProjectCreated`/`ProjectArchived`; it does not query Portfolio. Copy the few fields you need and
  accept they're eventually consistent.
- **No addresses in code.** The `ServiceBusClient` comes from the Aspire integration keyed to the
  AppHost `servicebus` resource — never a hardcoded namespace or connection string. New
  topics/subscriptions are declared as Aspire resources (and in the emulator's entity config), not
  invented at runtime.

Verify the Service Bus emulator surface (`AddAzureServiceBus(...).RunAsEmulator(...)`, the
entity-config format, `AddAzureServiceBusClient`) against https://aspire.dev — the transport binding
drifts; the outbox/inbox pattern itself is ours and stable.
