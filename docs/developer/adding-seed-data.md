# Adding Seed Data

*How to get development data into eight per-service databases across several tenants — and why seeding is one of only three places tenant filters are legitimately bypassed.*

**Related:** [ADR-0008](../adr/0008-shared-schema-tenantid-discriminator.md) · [Tenant Isolation](./patterns/tenant-isolation-defense-in-depth.md)

---

## The problem seeding creates

Every other write in this system happens inside a request or a message, with an ambient tenant. **Seeding has neither.** It runs at startup, outside any request, and it needs to create data for several tenants.

That means seeding is one of exactly three legitimate uses of `SystemTenantContext` (bypass mode) — the others being migration and the platform-admin surface. Everywhere else, a filter bypass is a defect.

## The rule

**Bypass to *choose* the tenant, never to ignore it.**

A seeder running under `SystemTenantContext` still sets `TenantId` explicitly on everything it creates. It bypasses the filter so it can write for tenant A and then tenant B in one process — not so it can write rows with no tenant at all.

A seeded row with `TenantId = Guid.Empty` is invisible to every subsequent query and is the most confusing possible bug to debug.

## Where seeders live

Each service owns its own: `PlannerPro.<Service>.Core/Seeding/<Service>SeedData.cs`, invoked from the host's startup path after migration. A service seeds **only its own database** — the same rule as everything else.

## What seeds where

| Service | Always | Development only |
| --- | --- | --- |
| Billing | The three **plans** (`free`, `team`, `business`) — they're global reference data | — |
| Access | — | A demo tenant, its settings, branding, owner membership |
| Portfolio | — | Sample clients, teams, projects |
| Planning | — | Sprints, goals, tasks tuned to demonstrate overload |
| Others | — | Whatever makes the service visible |

**Plans seed always** because they're platform reference data with no tenant. **Everything else seeds only in Development**, because seeding a demo tenant into production would be a data incident.

## Idempotency

Seeders run on **every startup**, against a database that persists between runs. They must therefore be idempotent — check-then-insert on a well-known id, never a blind insert.

Use fixed, well-known `Guid`s for demo entities so a re-run finds them, and so cross-service seed data lines up: Planning's seeded `ProjectReference` rows must use the same project ids Portfolio seeded, or the board renders against nothing.

## Seeding across service boundaries

This is the part that differs from a monolith. Portfolio seeds projects; Planning needs them in its read model. Two options:

1. **Let the events do it.** Portfolio's seeder publishes `ProjectCreated` through its outbox; Planning's consumer builds the read model. Slower to settle, but it exercises the real path — and if it doesn't work, that's a genuine finding.
2. **Seed both sides with matching well-known ids.** Faster and deterministic, but it means the seeded state is one the running system could never have produced.

**Prefer option 1 in development.** A seeder that bypasses the event path hides exactly the class of bug (a broken consumer, a missing envelope field) that seeding would otherwise surface for free.

## Demo data should demonstrate something

The single-tenant application seeded three sprints deliberately: sprint 1 healthy, **sprint 2 overloaded**, sprint 3 healthy — so the overload warning was visible the moment you opened the app. Keep that intent. Seed data that shows the product working is worth more than seed data that fills tables.

For a multi-tenant showcase, seed **at least two tenants** with different sprint cadences and different branding. One tenant proves nothing about isolation; two proves quite a lot, and makes a cross-tenant bug visible by eye.

## Re-seeding

Data persists between runs via the Postgres volume. To start clean, stop the system and remove the volume — and say so before suggesting it, because in a real deployment that's someone's planning data.

## Checklist

- [ ] Runs under `SystemTenantContext`, and sets `TenantId` explicitly on every row
- [ ] Idempotent — safe on every startup against a persistent database
- [ ] Plans seed always; tenant data seeds in Development only
- [ ] Well-known ids so re-runs find existing rows and cross-service data lines up
- [ ] Seeds **two or more tenants** with visibly different settings and branding
- [ ] Prefers the real event path over writing both sides directly
- [ ] Demo data demonstrates a product behaviour, not just row counts
