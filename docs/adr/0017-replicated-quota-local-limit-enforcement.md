# ADR-0017: Replicated quotas with local limit enforcement

- **Status:** Accepted
- **Date:** 2026-07-23
- **Deciders:** Robert Felkins
- **Related:** ADR-0002 (events), ADR-0004 (inbox), ADR-0014 (read models), ADR-0018 (plans), `.claude/rules/billing.md`

## Context

Plans carry limits: `MaxUsers`, `MaxClients`, `MaxProjects`, `MaxStorageMb`. Those limits are owned by Billing. The actions they constrain happen in Access (invite a member), Portfolio (create a client, create a project), and Files (upload an attachment).

The obvious implementation — ask Billing on each write — puts a synchronous cross-service call on four write paths. Creating a project would then fail whenever Billing is degraded, which is both the wrong availability trade and precisely the coupling ADR-0002 exists to avoid.

## Decision

**Billing owns and reconciles quotas; enforcing services keep a local quota snapshot and enforce against it.**

- Billing consumes the countable events (`InvitationAccepted`, `ClientCreated`/`ClientArchived`, `ProjectCreated`/`ProjectArchived`, `AttachmentUploaded`/`AttachmentDeleted` carrying byte size) and maintains per-tenant counters idempotently (ADR-0004).
- Billing publishes **`TenantQuotaChanged`** on plan change, status change, or material counter movement. Each enforcing service maintains a local snapshot of the limits it needs and checks that snapshot in its facade.
- Refusal is **`402` with a machine-readable limit code**, so the SPA shows a targeted upgrade prompt rather than a generic error.
- **Billing remains the authority** and reconciles from its counters; a local snapshot is a read model (ADR-0014), never a source of truth.
- **No synchronous call to Billing on any write path.**

## Consequences

**Positive**
- Creating a project doesn't depend on Billing being up. The write path owns its own availability.
- Enforcement is a local read — no added latency on a hot path.
- The pattern is the same read-model pattern used everywhere else, so it needs no new concepts.

**Negative**
- **There is a bounded overshoot window.** Between an action and the propagated quota, a tenant can exceed a limit — most visibly when several members create projects concurrently near the boundary. This is accepted, and the window must be documented and sized rather than pretended away. Reconciliation corrects the count; it does not un-create the project.
- Limits are therefore **soft in the small and hard in the large** — a product decision as much as a technical one, and it needs to be one the business is comfortable stating.
- Every enforcing service carries quota-snapshot plumbing and another consumer.
- Storage accounting depends on byte sizes travelling accurately on file events; a missed delete event leaks quota upward with no natural repair.

**Neutral**
- The overshoot window shrinks with dispatcher poll frequency — a tunable, not a fix.

## Alternatives considered

- **Synchronous check against Billing.** Hard limits with no overshoot, which is genuinely what a billing person wants. Rejected: couples four write paths to Billing's availability and adds latency to every create. **If overshoot ever becomes commercially unacceptable, this is the decision to revisit** — most likely as a synchronous check for the high-value limits only, keeping the async path for storage.
- **Each service counts its own rows and compares to a replicated limit.** Simpler, and avoids Billing counting at all. Rejected for cross-cutting limits like `MaxUsers` and `MaxStorageMb`, which no single service can count — and it splits the authority for usage reporting.
- **Enforce only at the UI.** Rejected; not enforcement.
- **A distributed counter (Redis) shared across services.** Removes overshoot without a synchronous service call, but introduces a shared mutable store across service boundaries — a shared database wearing a cache's clothes (ADR-0001).
