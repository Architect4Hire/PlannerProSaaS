# Frontend ↔ Gateway Integration

**Decided by:** [ADR-0007](../../adr/0007-path-based-tenancy-slug-routing.md), [ADR-0021](../../adr/0021-cookie-at-edge-internal-token.md), [ADR-0019](../../adr/0019-white-label-css-custom-properties.md)
**Rules:** [`.claude/rules/frontend.md`](../../../.claude/rules/frontend.md) · **Skill:** `add-component`

---

## Two invariants

1. **Zoneless.** There is no `zone.js`. The view updates because a **signal** changed — never because a promise settled. If the view isn't updating, find the signal you didn't write to; don't force a tick.
2. **One interceptor owns the tenant slug.** It rewrites `/api/x` → `/api/t/{slug}/x`, with an explicit passthrough list. **No store, no typed service, no component contains a slug.**

The second is the highest-leverage design choice in the client. Done right, adding a tenant-scoped call requires no tenancy code at all.

## The interceptor

```
/api/sprints/current   →  /api/t/acme/sprints/current
/api/auth/login        →  unchanged   (passthrough)
/api/public/…          →  unchanged   (passthrough)
/api/signup            →  unchanged   (passthrough)
/api/me/tenants        →  unchanged   (passthrough)
/api/admin/…           →  unchanged   (passthrough)
/api/ping              →  unchanged   (passthrough)
```

A call that must skip the rewrite belongs on the passthrough list — **change it there, once**, never by hardcoding a path in a store.

## Tenant state

`TenantContext` exposes `slug`, `tenant`, `role`, `plan`, `limits` as signals, populated by a resolver on the `/t/:tenant` parent route.

**Role is per-tenant.** There is no global `isAdmin` (ADR-0010) — `roleGuard(minRole)` and conditional UI both read `TenantContext.role`, never `Auth`.

`tenantGuard` handles an unknown or unauthorized slug, and must make both look **identical** — the client cannot reveal what the gateway deliberately hides.

## Data access

Reads use `httpResource` keyed off a signal. Writes patch local signals **optimistically** for instant feedback, then `reload()` reconciles with the server.

Because effort is computed server-side, **the server's recomputed total always wins**. A component's local state is never the source of truth for points.

Components never inject `HttpClient`; typed services do.

## Everything themeable comes from the server

Overload threshold, task-size warning, sprint cadence, plan limits, project colours, and the whole brand palette are **per-tenant data**. Hardcoding `24` in a template was correct in the single-tenant app and is a bug here.

Branding applies as CSS custom properties on `document.documentElement` — see [White-Label Theming](./white-label-theming.md).

## Handling `402`

A limit refusal carries a machine-readable code. Show the targeted upgrade prompt for that limit, not a generic error — that's the entire reason the code exists (ADR-0017).

## The known trap

A native `<select>` sets its initial value with **`[selected]` on each `<option>`**, not `[value]` on the select. Under zoneless change detection the latter does not reflect. This has bitten before; it is not a style preference.

## Standing rules

- Signals for state. No `zone.js`, `NgZone`, or manual change detection. No `any`.
- One interceptor owns the slug; nothing else knows it exists.
- Typed services only; components never call `HttpClient`.
- Role from `TenantContext`; thresholds, limits and colours from server data.
- Never edit component styles to theme something.
- Unknown slug and unauthorized slug look identical.
