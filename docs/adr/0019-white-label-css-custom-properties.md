# ADR-0019: White-label theming through CSS custom properties

- **Status:** Accepted
- **Date:** 2026-07-23
- **Deciders:** Robert Felkins
- **Related:** ADR-0007 (path-based tenancy), ADR-0011 (gateway resolution), `.claude/rules/frontend.md`

## Context

White-label is a headline capability: each tenant sees their own product name, logo, favicon, colours, and login tagline. The question is how a tenant's brand reaches the browser without forking the application or rebuilding per customer.

The existing SPA already defined its palette as CSS custom properties under `:root` — every component style already referenced variables rather than literal colours. That made one option dramatically cheaper than the rest.

## Decision

**Branding is delivered as CSS custom properties written onto `document.documentElement` at runtime.**

- The existing `:root` block in `styles.scss` becomes the **default theme**; a `Branding` service overrides individual properties per tenant. **No component styles change.**
- `TenantBranding` (Admin+) covers product name, logo, favicon, accent and core surface colours, login tagline, and theme mode.
- **`GET /api/public/tenants/{slug}/branding` is anonymous**, so the login page is branded *before* authentication — which is the whole point of white-label.
- That endpoint returns only public-safe fields, is rate-limited, and returns **generic defaults (not 404) for an unknown slug** so it cannot be used to enumerate tenants.
- **Contrast is validated on save** so a tenant cannot make their own application unreadable.
- Logo and favicon upload through Files to a `tenant-assets` container, reusing the existing type and size validation.

## Consequences

**Positive**
- Theming costs one service and a handful of variables; component CSS is untouched, so a new component is automatically themeable.
- The login page is branded, which is where white-label is actually judged.
- Adding a themeable property later is a variable, not a refactor.

**Negative**
- **The anonymous endpoint confirms that a slug exists** — the branding response for a real tenant differs from the generic default. This is an accepted, standard trade for branded login pages, but it is a genuine information leak and should be named as one rather than waved through.
- Theming is limited to what the variables express. A tenant wanting a materially different layout cannot have it, and that limit will be tested by a sales conversation eventually.
- A flash of default theme before branding loads is possible and needs handling.
- Contrast validation is a heuristic; it will occasionally block a choice a designer considers fine.

**Neutral**
- Branding is fetched per-slug, so the mechanism works unchanged if tenancy ever moves to subdomains or custom domains (ADR-0007).

## Alternatives considered

- **Per-tenant compiled CSS bundles.** Total control, and no runtime override. Rejected: a build per tenant is an operational burden that scales with the customer list.
- **Inline styles from a theme object.** Rejected: bypasses the cascade, breaks pseudo-states and media queries, and requires every component to participate.
- **Tenant-specific component overrides.** Rejected outright: it forks the application per customer, which is the thing white-label is supposed to avoid.
