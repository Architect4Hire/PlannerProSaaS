---
name: add-tenant-scoped-entity
description: >
  Add a new entity to a PlannerPro service and wire it into the multi-tenant isolation model — the
  ITenantScoped marker, the automatic query filter, tenant-scoped uniqueness constraints, and the
  migration. Use whenever the data model changes — e.g. "add a task label", "track sprint retro
  notes", "add a client contact". Every constraint and index here is a potential cross-tenant leak, so
  this skill exists to make the safe path the default one.
---

# Add a tenant-scoped entity

The rule this skill protects: **an entity holding tenant data implements `ITenantScoped`, and every
uniqueness constraint on it includes `TenantId`.** Miss either and you leak — the first silently, the
second through constraint violations that confirm another tenant's rows exist.

Read [`.claude/rules/tenancy.md`](../../rules/tenancy.md) first, and
[`.claude/rules/data-adjacent backend rules`](../../rules/backend.md) for layout.

## Decide first

- **Which service owns it?** The one whose database it belongs in. If two services seem to need it,
  it's one owner plus an integration event — never a shared table.
- **Is it genuinely tenant-scoped?** Almost everything is. The deliberate exceptions in this system
  are ASP.NET Identity tables (users are global) and `Plan` (the catalogue is platform-wide). If you
  believe a new entity should be global, say why explicitly — that's a defended decision, not a
  default.
- **Does it duplicate another service's data?** A small local read model fed by an event is correct
  (Planning's `ProjectReference`). A second copy of an authoritative table is not.

## Steps

1. **Define the entity** in `<Service>.Core/Managers/Models/Domain/`, one type per file, implementing
   `ITenantScoped`. Give it an XML doc comment saying what it means and why — the codebase is
   unusually well-commented and new types should meet that bar. `TenantId` is a `Guid`, is never set
   by application code on a normal path, and is stamped by the `SaveChanges` interceptor.

2. **Confirm the filter applies automatically.** The base `DbContext` reflects over `ITenantScoped`
   implementations in `OnModelCreating`. You should not be hand-writing a `HasQueryFilter` call — if
   you find yourself needing to, the mechanism has a gap and that's the thing to fix, in `Shared`.

3. **Make every uniqueness constraint tenant-scoped.** `HasIndex(e => new { e.TenantId, e.Slug })`,
   never `HasIndex(e => e.Slug)`. Existing examples: `Tenant.Slug` is globally unique because a tenant
   *is* the tenant; `Client.Slug` is `(TenantId, Slug)`; `Sprint.Number` is `(TenantId, Number)`;
   `SprintGoal` is `(TenantId, SprintId, ProjectId)`; `SprintCapacity` is `(TenantId, UserId,
   SprintId)`. Follow that pattern without exception.

4. **Keep filters consistent across the graph.** A filtered principal with unfiltered dependents
   produces EF warnings and real inconsistencies. Scope the whole aggregate, don't silence the
   warning.

5. **Pause for review.** Show the entity, its indexes, its relationships, and the intended migration
   name before generating anything.

6. **Generate the migration** from the host folder (the `DbContext` lives in `.Core`, but DI and
   config resolve in the host):
   `dotnet ef migrations add <Name> --project ../PlannerPro.<Service>.Core --startup-project . --context <Service>DbContext`

7. **Read the generated migration.** Confirm the indexes are tenant-scoped as intended, that nothing
   is being dropped and recreated unexpectedly, and that a new non-nullable column has a workable
   default for existing rows. Review only — don't hand-edit beyond that. Show me the diff before
   applying.

8. **Add the tests.** The reflection test that enumerates every `ITenantScoped` type and asserts a
   filter exists should now cover the new type automatically — confirm it does. Then add the
   cross-tenant case for whatever surface exposes it: tenant B using tenant A's id gets 404 and
   mutates nothing.

9. **Ripple outward if it's client-visible:** ServiceModel → endpoint → TS model → store → template.
   Skipping a step is a silent runtime break.

10. **Commit the migration with the entity change.** They are one change.

## Checklist before done
- [ ] Implements `ITenantScoped`; `TenantId` is a `Guid` and set by the interceptor, not by hand
- [ ] Query filter applied automatically by the base context — no hand-written filter
- [ ] **Every** unique index includes `TenantId`
- [ ] Filters consistent across the whole aggregate; no silenced EF warnings
- [ ] Migration reviewed before applying; no unintended drop-and-recreate; sane defaults
- [ ] Reflection test covers the new type; cross-tenant test added for its surface
- [ ] ServiceModel / TS model / store updated if client-visible
- [ ] `@tenant-isolation-auditor` run and clean; migration committed with the entity change
