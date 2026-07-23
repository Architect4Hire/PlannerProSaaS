# Tracing a Slice: Tenant Provisioning

*One request, start to finish, across every layer and four services. The best single orientation to how PlannerPro fits together.*

> ⚠️ **This walks the designed path, not observed code.** `src/` does not exist yet — Prompt 6 in the [prompt library](../prompts/scrub-prompts.md) builds this exact slice, deliberately, because it exercises the entire spine at once. **Once built, revise this doc to cite real files and line numbers, and delete this note.** If the built behaviour differs from what's below, the code is right and this document is wrong.

**Related:** [Layered Service Architecture](./patterns/layered-service-architecture.md) · [Transactional Outbox & Inbox](./patterns/transactional-outbox-and-inbox.md) · [Tenant Scope on the Bus](./patterns/tenant-scope-on-the-bus.md)

---

## Why this slice

Provisioning a tenant is the smallest request that touches everything: a platform-admin endpoint, a multi-entity transaction, an outbox write, the dispatcher, the bus, envelope-derived tenant scope, an idempotent consumer in another service, and the audit trail. If this works, the spine works.

## The path

```
Browser  POST /api/admin/tenants        (platform admin — tenants are provisioned, not signed up for)
   │
   ▼
Gateway ──────────────── platform route, NO tenant resolution (the tenant doesn't exist yet)
   │                     mints CorrelationId, strips any client-supplied copy
   ▼
Access · TenantAdminController ── binds ProvisionTenantViewModel, calls the facade
   │
   ▼
Access · TenantFacade ──────── validates: slug regex, reserved-word list, email uniqueness
   │
   ▼
Access · TenantBusiness ────── maps VM → Tenant, TenantSettings, TenantBranding, ApplicationUser,
   │                           TenantMembership(Owner); generates the sprint calendar from settings;
   │                           BUILDS TenantProvisioned with the full envelope
   ▼
Access · TenantDataLayer ───── ExecuteInTransactionAsync:
   │                             ├─ insert tenant + settings + branding + user + membership
   │                             └─ IOutbox.Enqueue(TenantProvisioned)
   │                           ONE transaction — all of it, or none of it
   ▼
201 Created + an invitation for the client's owner ─────────────▶ Browser
                                                                 (admin console; the owner arrives
                                                                  later at /app/t/{slug}/board)
```

**Note what does *not* happen here:** nothing is sent to the bus, and no other service is called. The response returns as soon as the transaction commits. Everything downstream is asynchronous.

## The asynchronous half

```
OutboxDispatcher (Access)
   ├─ polls unprocessed OutboxMessages, oldest first
   ├─ sends: MessageId = row Id, Subject = "TenantProvisioned",
   │         TenantId / CorrelationId / CausationId as application properties
   └─ stamps ProcessedOnUtc
   │
   ▼
Azure Service Bus (emulator)
   │
   ├──────────────────────────────┬──────────────────────────────┐
   ▼                              ▼                              ▼
Portfolio · ProcessorHost    Audit · ProcessorHost         Billing · ProcessorHost
   │ establishes ITenantContext      │ establishes scope            │ establishes scope
   │ FROM THE ENVELOPE               │ from envelope                │ from envelope
   ▼                                 ▼                              ▼
TenantProvisionedConsumer      AuditConsumer                 TenantProvisionedConsumer
   │ dedupe via inbox              │ dedupe via inbox              │ dedupe via inbox
   │ create "Internal" client      │ append immutable row          │ open a Trialing subscription
   │ (same transaction)            │ (same transaction)            │ + initial quota counters
   ▼                                 ▼                              ▼
portfoliodb                     auditdb                        billingdb
   │
   └─▶ publishes ClientCreated (its own outbox) → Billing counts it, Audit records it
```

**The step to stare at** is the processor host establishing `ITenantContext` from the envelope. The consumer that follows contains no tenancy code at all — it queries and writes normally, and the query filter and `SaveChanges` interceptor do the work. If that step were missing, the consumer would run, find nothing, write nothing, and report success. See [Tenant Scope on the Bus](./patterns/tenant-scope-on-the-bus.md).

## Failure modes worth understanding

| What fails | What happens |
| --- | --- |
| Validation (bad slug, reserved slug, taken email) | 400 before anything is written |
| A mid-transaction error in Access | Nothing commits — no tenant, **no outbox row**. The signup simply failed |
| Dispatcher crashes after send, before stamp | The row resends with the same `MessageId`; every consumer's inbox no-ops |
| Portfolio's consumer throws | The message isn't completed; it redelivers. The tenant exists with no default client until it succeeds |
| Portfolio is down entirely | Signup still succeeds. The client appears when Portfolio recovers |
| The event has no `TenantId` | **Dead-lettered.** Not processed under a guessed scope (ADR-0009) |

That fifth row is the point of the whole architecture: a client can be provisioned while Portfolio, Billing, Audit and Notifications are all down.

## What to verify when you build it

1. Provision **two** tenants from the admin surface. Each gets exactly one "Internal" client.
2. Redeliver a message manually. Confirm the consumer no-ops and no duplicate client appears.
3. Query the board as tenant B using tenant A's ids. Confirm 404 — same shape as a nonexistent id.
4. Confirm no anonymous `/api/signup` route exists; provisioning is platform-admin only.
5. Trace the whole fan-out by `CorrelationId` in the audit trail, and confirm the `CausationId` tree has `ClientCreated` hanging off `TenantProvisioned` rather than everything flat under the root.
