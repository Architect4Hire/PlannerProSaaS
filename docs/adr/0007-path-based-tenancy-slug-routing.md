# ADR-0007: Path-based tenancy (`/t/{slug}`) rather than subdomains

- **Status:** Accepted
- **Date:** 2026-07-23
- **Deciders:** Robert Felkins
- **Related:** ADR-0006 (gateway), ADR-0011 (tenant resolution), ADR-0019 (white-label), ADR-0021 (cookie at the edge)

## Context

A multi-tenant SaaS must express *which tenant* a request belongs to. The three common schemes are subdomain (`acme.plannerpro.app`), custom domain (`plan.acme.com`), and path prefix (`/t/acme`). The choice constrains DNS, TLS, CORS, and cookie policy — and reversing it later touches every URL in the product.

The single-tenant application already had a working same-origin setup: cookie authentication with `SameSite=Strict`, antiforgery on every mutation, no CORS. Preserving that was worth real weight, because loosening it is where multi-tenant auth bugs come from.

## Decision

**Tenancy is expressed in the path: `/t/{slug}` for app routes and `/api/t/{slug}/…` for API routes.**

- The gateway resolves the slug (ADR-0011); no service parses it.
- Slug rules: `^[a-z0-9][a-z0-9-]{1,30}[a-z0-9]$`, plus a reserved list (`api`, `auth`, `admin`, `app`, `www`, `t`, `signup`, `login`, `health`, `public`, `assets`, `static`).
- Anonymous and user-scoped routes deliberately sit outside the tenant segment: `/api/ping`, `/api/public/*`, `/api/auth/*`, `/api/signup`, `/api/invitations/*`, `/api/me/tenants`, `/api/admin/*`.
- In the SPA, **one interceptor** rewrites `/api/x` to `/api/t/{slug}/x` with an explicit passthrough list, so no store or typed service contains a slug.

## Consequences

**Positive**
- **Single origin.** No DNS work, no wildcard certificate, no CORS policy, and the existing `SameSite=Strict` cookie plus antiforgery setup keeps working unchanged. This is the decisive advantage.
- A tenant switcher is a route change, not a redirect across origins.
- Custom domains remain a future *additive* option rather than a rewrite.

**Negative**
- The tenant is visible and editable in the URL, so isolation rests entirely on server-side membership checks — a user *will* try editing the slug. That is by design and covered by ADR-0011 (404, never 403), but it means the check can never be relaxed.
- Slugs share a namespace with routes, hence the reserved list. Forgetting an entry creates an unroutable tenant.
- Less "enterprise" feeling than a subdomain; a real objection for some buyers.
- One cookie serves all tenants for a user, so a compromised session spans every tenant they belong to.

**Neutral**
- Branding is fetched per-slug and applied at runtime (ADR-0019), which works identically under any of the three schemes.

## Alternatives considered

- **Subdomain per tenant.** Better isolation feel, per-tenant cookies, and a natural fit for custom domains later. Rejected for the DNS, wildcard-TLS, and CORS cost, and because it would force reworking a cookie/antiforgery setup that already worked.
- **Custom domains.** A genuine enterprise requirement, but a much larger operational surface (certificate issuance and renewal per tenant). Explicitly out of scope; path-based routing does not block adding it later.
- **Header-based tenancy (`X-Tenant`).** Clean for an API, unusable for a browser app that needs bookmarkable, shareable URLs.
