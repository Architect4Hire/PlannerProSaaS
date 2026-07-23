# Read-Through Caching

**Decided by:** [ADR-0012](../../adr/0012-aspire-local-first-emulators.md) (Redis as a local resource); pattern owned by the facade layer ([ADR-0005](../../adr/0005-thin-host-core-layered-library.md))

---

## Where it lives

**The facade owns caching.** Not the controller, not business, not the repository. The facade is the layer that returns ServiceModels, so it is the layer that can return a cached one.

```
Controller ──▶ Facade ──▶ cache hit?  ──yes──▶ return cached ServiceModel
                            │
                            no
                            ▼
                        Business ──▶ Data ──▶ Repository ──▶ DB
                            │
                            └──▶ populate cache, return
```

## The cache key must include `TenantId`

This is the part that differs from a single-tenant system, and getting it wrong is not a stale-data bug — it is a **cross-tenant leak with a fast path**.

```
❌  board:sprint:42
✅  board:{tenantId}:sprint:42
```

A tenant-blind key means tenant B's request can be served tenant A's cached board, bypassing every query filter in the system because no query runs at all. The [isolation layers](./tenant-isolation-defense-in-depth.md) do not protect a cache.

Treat any cache key without a tenant segment as a critical finding.

## Fail open

A cache is an optimization, not a dependency. If Redis is unavailable, the read falls through to the database and the request succeeds — slower. A caching layer that turns a cache outage into an application outage has inverted its purpose.

Failures are logged, not thrown.

## Invalidation

Writes invalidate the keys they affect. The honest options:

- **Explicit invalidation** on write — precise, but every new write path is a chance to forget one.
- **Generation token** per tenant per entity family — bump a counter on write, include it in the key, and stale entries age out naturally. More robust against forgetting; costs one extra read.

Whichever is used, **the generation token is itself tenant-scoped**.

## What to cache

Hot reads whose cost is real: the sprint board, the timeline. Not writes, not anything user-specific unless the key says so, and not anything where staleness would be confusing on a screen the user is actively editing.

Because effort totals are computed rather than stored, a cached board is a cached *computation* — which makes invalidation on task changes non-negotiable.

## Standing rules

- The facade caches; no other layer does.
- **Every cache key includes `TenantId`.**
- Fail open — a cache outage degrades performance, never availability.
- Invalidate on write, or use a tenant-scoped generation token.
- Never cache across a tenant boundary, including "global" lookups that happen to be tenant-filtered.
