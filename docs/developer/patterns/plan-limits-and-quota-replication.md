# Plan Limits & Quota Replication

**Decided by:** [ADR-0017](../../adr/0017-replicated-quota-local-limit-enforcement.md), [ADR-0018](../../adr/0018-plan-model-now-stripe-later.md)
**Rules:** [`.claude/rules/billing.md`](../../../.claude/rules/billing.md)

---

## The problem this solves

Limits (`MaxUsers`, `MaxClients`, `MaxProjects`, `MaxStorageMb`) are owned by Billing. The actions they constrain happen in Access, Portfolio, and Files. The obvious implementation — ask Billing on each write — puts a synchronous cross-service call on four write paths, so creating a project fails whenever Billing is degraded.

That's the wrong availability trade, and it's the coupling ADR-0002 exists to avoid.

## The mechanism

```
Portfolio: ProjectCreated ──▶ bus ──▶ Billing consumer
                                        ├─ dedupe via inbox
                                        └─ increment counter
                                              ↓
                              TenantQuotaChanged ──▶ bus
                                              ↓
Portfolio: local quota snapshot updated (a read model)
                                              ↓
Next create: facade reads the LOCAL snapshot — no call to Billing
   ├─ under limit → proceed
   └─ at limit    → 402 { limit: "MaxProjects" }
```

Billing consumes the countable events (`InvitationAccepted`, `ClientCreated`/`ClientArchived`, `ProjectCreated`/`ProjectArchived`, `AttachmentUploaded`/`AttachmentDeleted` with byte size) and remains the **authority**. Each enforcing service holds a snapshot and enforces against it.

This is just [the read-model pattern](./database-per-service-and-data-ownership.md) applied to quotas — no new concepts.

## The overshoot window

**Between an action and the propagated quota, a tenant can exceed a limit.** Two admins creating projects concurrently near the boundary can both pass a stale snapshot.

This is accepted, not hidden. Three things follow:

1. **Measure and document the window.** It's a function of dispatcher poll frequency and propagation latency. An unsized window is an unmanaged risk.
2. **Reconciliation corrects the count, not the outcome.** Billing will show the true number; it will not un-create the project.
3. **Limits are soft in the small, hard in the large.** That is a *product* statement the business must be comfortable making, not just an engineering detail.

If overshoot ever becomes commercially unacceptable, ADR-0017 is the decision to revisit — most likely as a synchronous check for high-value limits only.

## Refusal shape

`402 Payment Required` with a **machine-readable limit code**, so the SPA shows a targeted upgrade prompt rather than a generic error. The code is the contract: `MaxUsers`, `MaxClients`, `MaxProjects`, `MaxStorageMb`.

## Storage accounting

Byte sizes travel on `AttachmentUploaded`/`AttachmentDeleted`. **A missed delete event leaks quota upward with no natural repair path** — the same reconciliation gap as any read model (risk #7).

## Lifecycle

`Trialing → Active → PastDue → Suspended → Cancelled`. Suspended and Cancelled are **read-only, not locked out** (ADR-0020) — users can still see and export their data. Suspension is never an authentication failure.

Stripe columns exist and stay `NULL` (ADR-0018). No provider is wired.

## Standing rules

- No synchronous call to Billing on any write path.
- Enforce against the local snapshot; Billing reconciles.
- Refuse with `402` and a machine-readable code.
- Counters are maintained idempotently — a double-delivered event must not double-count.
- Suspended means read-only.
