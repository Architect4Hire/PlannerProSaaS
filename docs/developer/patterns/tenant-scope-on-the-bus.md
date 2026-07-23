# Tenant Scope on the Bus

**Decided by:** [ADR-0009](../../adr/0009-tenant-scope-from-event-envelope.md), with [ADR-0013](../../adr/0013-contracts-leaf-tenant-envelope.md)
**Rules:** [`.claude/rules/messaging.md`](../../../.claude/rules/messaging.md), [`.claude/rules/tenancy.md`](../../../.claude/rules/tenancy.md)

---

## Why this needs its own document

Every other pattern here has a counterpart in the single-tenant application. This one doesn't, and it is the mechanism most likely to fail silently.

Query filters close over a **scoped** `ITenantContext`, populated by middleware from the gateway's headers. That covers every HTTP request in the system. **A Service Bus consumer is not an HTTP request.** It runs on a background thread, in a scope the processor host creates, with no headers, no route values, and no ambient tenant.

## The two silent failures

**Failure A — unset context.** `ITenantContext.TenantId` is `Guid.Empty`. Every query filter matches nothing. The consumer runs, finds no rows, writes nothing, and completes successfully. Read models go stale. Quota counters stop incrementing. **No exception is thrown and no log line looks wrong.** This is discovered weeks later when someone notices the board is missing a project.

**Failure B — defaulted bypass.** `BypassFilters` defaults to `true` for background work "because seeding needs it." Consumers now read and write across **every tenant in the database**. Also silent, and considerably worse.

Both failures come from the same root: there is no ambient source of truth on the bus. The only thing that knows which tenant a message belongs to is the message.

## The mechanism

```
Business builds event (stamps TenantId, CorrelationId, CausationId, actor)
    ↓  same transaction as the domain write
OutboxMessages row (carries TenantId)
    ↓  OutboxDispatcher — promotes TenantId to a Service Bus application property
Service Bus
    ↓
ServiceBusProcessorHost
    ├─ reads TenantId from the envelope
    ├─ opens a DI scope
    ├─ populates ITenantContext          ← THE CRITICAL STEP
    └─ resolves and invokes the consumer
    ↓
Consumer — queries and writes normally; filters and interceptor do the work
```

Four properties make this safe:

1. **`IIntegrationEvent` mandates `TenantId`.** An event type without one does not compile (ADR-0013). The envelope cannot be forgotten at definition time.
2. **Scope establishment is central.** It happens in the processor host, once, for every consumer. A consumer author cannot forget it because a consumer author never does it.
3. **Consumers contain no tenancy code.** They look exactly like request-path code. If you find tenancy plumbing in a consumer, something has gone wrong upstream.
4. **A message with no `TenantId` is dead-lettered**, not processed under a guessed or default scope. Failing loudly beats writing to the wrong tenant.

## Debugging checklist

When a consumer "runs but does nothing":

1. Is `TenantId` populated on the message's application properties? (Check the dashboard, not the body.)
2. Did the processor host establish `ITenantContext` before invoking? Is `IsResolved` true inside the consumer?
3. Is the consumer querying an `ITenantScoped` entity whose filter is therefore matching nothing?
4. Did the publisher stamp `TenantId` when building the event, or is it `Guid.Empty` all the way through?

The symptom of a tenancy scope problem is almost always "no data," not "wrong data" — which is why it reads as a query bug and gets debugged in the wrong place.

## What not to do

- **Don't resolve tenancy inside a consumer.** Not from the payload body, not by looking up an entity, not from configuration.
- **Don't `IgnoreQueryFilters()` in a consumer** to make it "work." If that fixes it, scope was never established and you have just turned off the defense at the point nobody is watching.
- **Don't default `BypassFilters` for background work.** Seeding and migration have their own explicit contexts.
- **Don't publish an event without `TenantId`** because today's consumer doesn't need it. Tomorrow's will, and the compiler already prevents it.

## Platform-level events

Some events legitimately span tenants — a platform admin suspending several tenants, or a scheduled job. These don't fit the model and must run under an explicit `SystemTenantContext`, narrow and commented. They are the exception; treat every one as needing justification.

## Standing rules

- Every integration event carries `TenantId`; the compiler enforces it.
- Scope comes from the envelope, established centrally, before invocation.
- A message without `TenantId` is dead-lettered.
- Consumers contain no tenancy logic and write only their own service's database.
- Changes to the processor host's scoping are **high-risk** — every consumer in every service depends on them.
