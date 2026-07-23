# ADR-0010: Global identity with tenant membership

- **Status:** Accepted
- **Date:** 2026-07-23
- **Deciders:** Robert Felkins
- **Related:** ADR-0008 (discriminator & filters), ADR-0011 (gateway resolution), ADR-0021 (cookie at the edge), `.claude/rules/tenancy.md`

## Context

PlannerPro is sold on an agency model: `Tenant → Client → Project`. The people who use it are frequently consultants who work with several organizations. If accounts were scoped per tenant, one person would hold several passwords for the same product and could never switch between organizations without signing out.

The single-tenant application had `ApplicationUser.IsAdmin` and `ApplicationUser.DefaultCapacityPoints` as global flags on the user. Both are inherently per-tenant: someone can be an owner of their own org and a viewer in a client's.

## Decision

**One `ApplicationUser` per email address across the whole platform. Tenancy is expressed by `TenantMembership` rows.**

- Identity tables are **global and deliberately excluded from tenant query filters**. Users are not tenant-scoped data.
- `TenantMembership` (`TenantId`, `UserId`, `Role`, `Status`, `DefaultCapacityPoints`) **is** tenant-scoped, unique on `(TenantId, UserId)`.
- `IsAdmin` and `DefaultCapacityPoints` move off the user and onto the membership. A separate `IsPlatformAdmin` flag stays on the user for staff-level access (ADR-0006 platform surface).
- **Any query enumerating a tenant's people goes through `TenantMemberships` joined to `Users`** — never `db.Users` directly, which would enumerate the entire platform.
- A tenant must always retain at least one active Owner; enforced on every membership change path.

## Consequences

**Positive**
- A tenant switcher is free, and matches how consultants and agencies actually work.
- One credential per person; no duplicate accounts to reconcile when someone joins a second org.
- Role is per-tenant, which is what the role matrix actually needs.

**Negative**
- **`accessdb` is the one database with mixed scoping** — some tables filtered, some deliberately not. That is a genuine cognitive hazard and the most likely place someone "fixes" a missing filter that was intentional. It is commented in code and called out in `.claude/rules/tenancy.md`; the mitigation is documentation and review, which is weaker than a mechanism.
- `db.Users` becomes a footgun. The `tenancy-guard.sh` hook warns on it.
- A compromised account exposes every tenant that user belongs to (compounding ADR-0007's shared cookie).
- Email enumeration at signup is now platform-wide rather than tenant-scoped.

**Neutral**
- Users are global but memberships are scoped, so "who is in this tenant" is always a join, never a scan.

## Alternatives considered

- **Tenant-scoped users** (one account per person per tenant). Simpler mental model, and every table is uniformly filtered — a real benefit. Rejected: it breaks the consultant use case, forces duplicate credentials, and makes the tenant switcher impossible.
- **External identity provider (Entra ID, Auth0) with tenant mapping.** The right answer for enterprise SSO and explicitly out of scope for now. This decision does not block it: memberships would map to external identities instead of local users.
- **Keep `IsAdmin` on the user.** Rejected outright: it is per-tenant by nature, and leaving it global would grant admin in every tenant a user joins.
