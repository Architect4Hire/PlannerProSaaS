---
name: add-component
description: >
  Scaffold a new Angular component in the PlannerPro frontend. Use when creating UI — e.g. "add a
  client filter", "make a plan-usage card", "build the branding editor". Produces a standalone,
  zoneless, signal-first component wired to a typed service that calls the GATEWAY (never a service
  directly, never with a hardcoded tenant slug), following this repo's frontend conventions.
---

# Add an Angular component

Work in `src/web/`. Two things define this frontend: it is **zoneless** (the view updates because a
signal changed, never because a promise settled), and it talks to exactly one backend address — the
**gateway** — with the tenant slug injected by **one interceptor**. Protect both.

Read [`.claude/rules/frontend.md`](../../rules/frontend.md) first.

1. **Generate.** `ng generate component features/<name>` — standalone, kebab-case files, one folder
   per feature.
2. **Signals, not zones.** `input()` / `model()` for inputs, `signal` / `computed` for state. No
   `zone.js`, `NgZone`, or manual change detection. If the view isn't updating, find the signal you
   didn't write to.
3. **Types.** Reuse or extend the interfaces mirroring the owning service's **ServiceModels** exactly.
   No `any`. Enums arrive as strings, so they're string unions. `Guid` is `string`.
4. **Data access through a typed store/service, never `HttpClient` in a component.** Reads via
   `httpResource`; writes patch local signals optimistically then `reload()` to reconcile — the server
   recomputes effort and wins.
5. **Never write a tenant slug into a URL.** Call `/api/x`; the interceptor rewrites it to
   `/api/t/{slug}/x`. A slug in a store is the exact thing that design prevents. If a call must skip
   the rewrite, it belongs on the interceptor's passthrough list — change it there, once.
6. **Role checks read `TenantContext.role`.** Role is per-tenant; there is no global `isAdmin`. Use
   `roleGuard(minRole)` for routes and the same signal for conditional UI.
7. **Everything themeable comes from the server.** Overload threshold, task-size warning, plan limits,
   project colors, and the brand palette are per-tenant data. Theme via CSS custom properties on
   `document.documentElement` — **never** edit component styles to brand something.
8. **A native `<select>` sets its initial value with `[selected]` on each `<option>`,** not `[value]`
   on the select. Under zoneless change detection the latter does not reflect.
9. **Handle the limit response.** A `402` carries a machine-readable limit code — show the targeted
   upgrade prompt, not a generic error.
10. **Tests.** Update `.spec.ts` with a render test and one behavior test; mock the typed service
    (assert the un-rewritten `/api/...` path), don't hit the network. Run `ng test`.

## Checklist before done
- [ ] Standalone, kebab-case filenames, lazy route, guarded appropriately
- [ ] Signals for state; no `zone.js` / `NgZone` / manual change detection; no `any`
- [ ] Types mirror ServiceModels; enums as string unions
- [ ] Data through a typed store; **no tenant slug anywhere outside the interceptor**
- [ ] Role read from `TenantContext`; thresholds, limits and colors read from server data
- [ ] `<select>` initial value via `[selected]`; `402` handled with a targeted prompt
- [ ] Tests pass with the service mocked (`ng test`)
