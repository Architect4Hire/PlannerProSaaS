---
paths:
  - src/web/**
---
# Frontend rules — Angular 22, zoneless, signal-first

The app is **zoneless** — nothing re-renders because a promise settled; the view updates because a
**signal** changed. And it talks to exactly one backend address, the **gateway**. Those two facts are
the rules to protect.

- **Zoneless, signal-first.** State lives in signals (`signal`, `computed`). No `zone.js`, no
  `NgZone`, no manual `detectChanges()`. If the view isn't updating, find the signal you didn't write
  to — don't force a tick.
- **Standalone components, no NgModules.** One feature per folder, kebab-case filenames, PascalCase
  classes, lazy routes via `loadComponent`.
- **Tenancy lives in exactly two places.** `TenantContext` (signals: `slug`, `tenant`, `role`, `plan`,
  `limits`), populated by a resolver on the `/t/:tenant` parent route; and **one interceptor** that
  rewrites `/api/x` → `/api/t/{slug}/x`, with an explicit passthrough list (`/api/auth`,
  `/api/public`, `/api/signup`, `/api/me`, `/api/admin`, `/api/ping`). Done right, **no store or
  typed service needs a tenant-aware URL** — keep it that way. A slug hardcoded in a store is exactly
  what this design prevents.
- **Reads use `httpResource`; writes go through a store.** Optimistic local patch for instant
  feedback, then `reload()` to reconcile — the server recomputes effort and wins. Never let component
  state become the source of truth for points.
- **Strict TypeScript. No `any`.** Model interfaces mirror the owning service's **ServiceModels**
  exactly: C# `PascalCase` → TS `camelCase`, `T?` → `T | null`, `DateOnly`/`DateTime` → `string`,
  `List<T>` → `T[]`. Enums arrive as **strings**, so model them as string unions.
- **Data access through typed services,** one per backend resource, all hitting the **gateway** base
  URL from Aspire-injected config. Components never call `HttpClient` directly. Never target a
  service directly, and never a literal host:port.
- **Guards:** `authGuard` for authentication, `tenantGuard` for an unknown or unauthorized slug (which
  must look the same as not existing), and a `roleGuard(minRole)` factory reading `TenantContext.role`.
  Role is per-tenant — there is no global `isAdmin`.
- **Everything themeable comes from the server.** Overload threshold, task-size warning, sprint
  cadence, plan limits, project colors, and the whole brand palette are per-tenant data. Branding is
  applied by writing CSS custom properties onto `document.documentElement`; the `:root` block in
  `styles.scss` is the default theme. **Never edit component styles to theme something.**
- **A native `<select>` sets its initial value with `[selected]` on each `<option>`,** not `[value]`
  on the select — under zoneless change detection the latter does not reflect. Known trap, not a
  preference.
- **Scaffolding.** `ng generate component features/<name>` (or the `add-component` skill) so structure
  stays consistent.

Screens: board, timeline, roadmap, capacity, login, signup, invite-accept, tenant switcher, plus the
admin surfaces (clients, teams, members, settings, branding, plan/usage) and the platform console.
