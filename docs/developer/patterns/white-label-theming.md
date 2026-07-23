# White-Label Theming

**Decided by:** [ADR-0019](../../adr/0019-white-label-css-custom-properties.md)
**Rules:** [`.claude/rules/frontend.md`](../../../.claude/rules/frontend.md)

---

## The mechanism

The SPA's palette is already defined as CSS custom properties under `:root` — every component style references a variable, never a literal colour. That existing block becomes the **default theme**, and a `Branding` service overrides individual properties on `document.documentElement` at runtime.

```
Tenant resolved  ──▶  Branding service  ──▶  document.documentElement.style
                                              .setProperty('--accent', '#c2410c')
                                              .setProperty('--bg', …)
```

**No component styles change. Ever.** That is the whole point: a new component is automatically themeable because it uses the same variables everything else does.

## What's brandable

Product name (replacing "PlannerPro" in the topbar, `<title>`, and the login page), logo, favicon, accent and core surface colours, login tagline, theme mode. Admin+ only.

Logo and favicon upload through Files to a `tenant-assets` container, reusing the same type and size validation as task attachments.

## The anonymous endpoint

`GET /api/public/tenants/{slug}/branding` is **anonymous**, because the login page must be branded *before* authentication — which is where white-label is actually judged.

Three constraints on it:

- Returns **only public-safe fields**. No member counts, no emails, no plan.
- Returns **generic defaults, not 404**, for an unknown slug.
- **Rate-limited.**

> ⚠️ **This endpoint still confirms a slug exists.** A real tenant's branding differs from the generic default, so it can be probed. ADR-0019 accepts this as a standard trade for branded login pages — but it is a genuine information leak and should be named as one rather than waved through.

## Load order

| Context | Source |
| --- | --- |
| Login page | Anonymous endpoint, by slug from the URL |
| Inside the app | Authenticated bootstrap on tenant resolve |

A flash of default theme before branding applies is possible and needs handling — inline the critical variables or hold the first paint.

## Contrast validation

Validated **on save**, so a tenant cannot make their own application unreadable. It's a heuristic and will occasionally block something a designer considers fine; that's the accepted cost of not shipping a support ticket instead.

## The limit

Theming is bounded by what the variables express. A tenant wanting a materially different *layout* cannot have it. That limit will be tested by a sales conversation eventually, and the answer needs to be a product decision rather than a per-customer CSS fork — which is precisely what white-label exists to avoid.

## Standing rules

- Theme through CSS custom properties on `document.documentElement`.
- **Never** edit component styles to brand something.
- Project accent colours come from data (`Project.ColorHex`), not a hardcoded palette.
- The anonymous endpoint returns public-safe fields, generic defaults for unknown slugs, and is rate-limited.
- Validate contrast on save.
