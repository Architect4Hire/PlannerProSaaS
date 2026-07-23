# Database-per-Service & Data Ownership

**Decided by:** [ADR-0001](../../adr/0001-microservices-database-per-service.md), [ADR-0014](../../adr/0014-cross-service-read-model-strategy.md), with [ADR-0008](../../adr/0008-shared-schema-tenantid-discriminator.md)

---

## Two boundaries, two mechanisms

This trips people up, so state it plainly:

| Boundary | Mechanism | Strength |
| --- | --- | --- |
| **Between services** | Separate physical databases | Physical — there is no connection string to the other one |
| **Between tenants** | `TenantId` discriminator + query filters | Logical — enforced by code, defended in layers |

Services never share a database. Tenants *do* share a database, within a service. That is not an inconsistency; they are different problems with different cost profiles (ADR-0008 explains why per-tenant databases were rejected at eight services).

## Who owns what

| Service | Owns |
| --- | --- |
| Access | Accounts (**global**), tenants, settings, branding, memberships, invitations |
| Portfolio | Clients, teams, team members, projects |
| Planning | Sprints, sprint goals, tasks, capacity |
| Roadmap | Roadmap goals |
| Files | Attachments, tenant assets |
| Billing | Plans (**global**), subscriptions, usage counters |
| Audit | The append-only trail |
| Notifications | The delivery log |

## When you need another service's data

The board shows projects. Projects belong to Portfolio; the board belongs to Planning. Three options, one answer:

1. **Second connection string.** Forbidden. This is the coupling the whole architecture exists to prevent.
2. **Synchronous call.** Forbidden on read paths that matter — the board would fail whenever Portfolio does.
3. **Consume the event, keep a local read model.** ✅ Planning keeps a `ProjectReference` (id, name, colour, client, active) fed by `ProjectCreated`/`ProjectArchived`.

**Read models hold only the fields actually rendered or needed for a decision** — not a mirror of the source table. The owning service stays authoritative; a read model never drives a write back into the owner.

## The costs, stated honestly

- **Duplicated and eventually consistent.** A renamed project shows the old name until the event lands — visible to a user on a screen they're looking at.
- **A missed or dead-lettered event leaves a read model silently wrong**, with no natural repair path. **No reconciliation mechanism exists yet.** This is an open gap, not a solved problem (ADR-0014, risk #7).
- Every read model is another consumer to write, test for idempotency, and keep in step with the source's event shape.

## When query composition is still fine

ADR-0014 rejects gateway-level fan-out for the *board*, because it re-couples availability on a hot path. It does not forbid it for rare admin screens where freshness matters more than resilience. Use judgement, and say which you're choosing and why.

## Standing rules

- One database per service; never a second connection string.
- Tenants share a database within a service, separated by `TenantId`.
- Need another service's data → consume its event and keep a local copy.
- Read models are small, tenant-scoped, and never authoritative.
- A consumer writes only its own service's database.
