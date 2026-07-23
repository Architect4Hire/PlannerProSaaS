# ADR-0021: Cookie at the browser edge, internal token behind the gateway

- **Status:** Accepted
- **Date:** 2026-07-23
- **Deciders:** Robert Felkins
- **Related:** ADR-0006 (gateway), ADR-0007 (path-based tenancy), ADR-0010 (global identity), ADR-0011 (header projection)

## Context

The single-tenant application authenticated with an ASP.NET Core Identity cookie: `HttpOnly`, `SameSite=Strict`, `Secure`, with antiforgery on every mutation, served same-origin. That setup is secure, well understood, and — thanks to path-based tenancy (ADR-0007) — still viable.

Behind the gateway, eight services need to know who is calling. Passing the browser's cookie through would make every service depend on Identity's cookie configuration and give each one the ability to impersonate the user's full session.

## Decision

**The browser authenticates with a cookie at the gateway; the gateway establishes trusted identity inward.**

- The SPA keeps the existing cookie plus antiforgery model, unchanged, same-origin (ADR-0007).
- The gateway validates the cookie, then projects actor, role, tenant, and correlation as trusted headers (ADR-0011). Client-supplied copies are stripped.
- Services never see the browser cookie and never validate it.
- Signing material and secrets stay out of source — user-secrets in development, environment variables in production.

## Consequences

**Positive**
- The proven same-origin cookie and antiforgery setup survives the architecture change intact — a meaningful risk reduction, since auth rewrites are where multi-tenant bugs live.
- Services are simple: identity arrives ambiently, exactly as tenancy does, on both the HTTP and bus paths.
- No token handling in the SPA, so no token storage question and no XSS-exfiltration surface for a bearer token.

**Negative**
- **Services trust headers, which are not self-verifying.** A service reachable off-gateway trusts anything it is told. This is the same exposure as ADR-0011 and the same mitigation applies — network-level enforcement, currently unimplemented and tracked as a top risk.
- One cookie covers every tenant a user belongs to, so a stolen session spans all of them (compounding ADR-0007 and ADR-0010).
- A future non-browser client (a public API, a mobile app) has no story here and would need a token scheme added.

**Neutral**
- `PlannerPro.Access` issues and validates credentials; the gateway consumes that decision rather than re-implementing it.

## Alternatives considered

- **A signed internal JWT minted by the gateway per request, carrying tenant, role and actor.** Self-verifying, so a service could trust it without trusting the network — which addresses this ADR's main weakness directly. Rejected *for now* only because header projection is simpler and ships sooner. **This is the most likely successor to both this ADR and ADR-0011**, and the two should be revisited together.
- **JWT to the browser instead of a cookie.** Rejected: introduces token storage, refresh, and XSS exfiltration risk, in exchange for benefits a same-origin app doesn't need.
- **Pass the browser cookie through to services.** Rejected: couples every service to Identity's configuration and hands each one full session authority.
