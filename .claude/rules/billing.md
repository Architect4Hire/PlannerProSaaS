---
paths:
  - src/PlannerPro.Billing/**
  - src/PlannerPro.Billing.Core/**
  - src/PlannerPro.*.Core/Facade/**
---
# Billing rules — plans, limits, and the quota replication contract

Billing owns plans, per-tenant usage counters, and the subscription lifecycle. It is the **authority**
on limits — but it is deliberately **not** in the write path of the services that enforce them.

- **Enforcement is local, against a replicated quota.** A service cannot synchronously ask Billing
  "am I under my project limit?" without coupling its write path to another service's availability.
  So Billing publishes `TenantQuotaChanged`, each enforcing service keeps a **local quota snapshot**,
  and enforcement reads that snapshot. Billing reconciles from the countable events and stays
  authoritative.
- **This is eventual consistency with a bounded overshoot window, on purpose.** A tenant can briefly
  exceed a limit between the counted event and the propagated quota. That is an accepted trade for
  keeping the write path independent — document the window, don't pretend it isn't there, and don't
  "fix" it with a synchronous call.
- **Counters are maintained from events, idempotently.** `InvitationAccepted` → users;
  `ClientCreated`/`ClientArchived` → clients; `ProjectCreated`/`ProjectArchived` → projects;
  `AttachmentUploaded`/`AttachmentDeleted` (carrying byte size) → storage. Delivery is at-least-once,
  so a double-delivered event must not double-count — dedupe via the inbox in the same transaction as
  the counter update.
- **Refuse with `402` and a machine-readable code.** `MaxUsers`, `MaxClients`, `MaxProjects`,
  `MaxStorageMb` — so the SPA can show a targeted upgrade prompt rather than a generic error.
- **Lifecycle:** `Trialing → Active → PastDue → Suspended → Cancelled`. Suspended and Cancelled
  tenants are **read-only**, not locked out: they can still see and export their data. That's better
  product behavior and less support load. Cancelled tenants purge after a retention window.
- **Stripe is modelled and inert.** `StripeCustomerId`, `StripeSubscriptionId` and
  `CurrentPeriodEndsAt` exist and stay `NULL`. Adding Checkout later means adding a webhook that
  writes those three columns and `Tenant.Status` — no schema or logic refactor. Do not wire a real
  payment provider.
- **Seeded plans:** `free` (3 users / 1 client / 3 projects / 100 MB), `team` (15 / 10 / 30 / 5 GB),
  `business` (100 / unlimited / unlimited / 50 GB). Prices are a product decision, not a code one.
- **Whether Viewer-role members count against `MaxUsers` is an open product question** — raise it
  rather than deciding it silently.
