# Authentication & Identity Propagation

**Decided by:** [ADR-0021](../../adr/0021-cookie-at-edge-internal-token.md), [ADR-0010](../../adr/0010-global-identity-tenant-membership.md), [ADR-0011](../../adr/0011-gateway-tenant-resolution-header-projection.md)

---

## The chain

```
Browser  ──cookie (HttpOnly, SameSite=Strict, Secure)──▶  Gateway
Gateway  ──validate cookie, resolve membership──────────▶  trusted headers
Service  ──read headers into ITenantContext───────────▶  business layer actor
```

Services never see the browser cookie and never validate it. Identity arrives ambiently, exactly as tenancy does — and on the bus path, both arrive from the event envelope instead.

## Why a cookie survived the rewrite

The single-tenant app used an Identity cookie with antiforgery on every mutation, served same-origin. Path-based tenancy (ADR-0007) keeps the app single-origin, so that setup kept working **unchanged**. Auth rewrites are where multi-tenant bugs live; not doing one was a deliberate risk reduction.

The SPA holds no token, so there is no token storage question and no bearer token for XSS to exfiltrate.

## Identity is global, roles are per-tenant

One `ApplicationUser` per email **platform-wide** (ADR-0010). A consultant working with three organizations is one account with three `TenantMembership` rows. Consequences that bite:

- **There is no global `isAdmin`.** Role lives on the membership. The SPA reads it from `TenantContext`, never from `Auth`.
- **`db.Users` enumerates the entire platform.** Any "who is in this tenant" query goes through `TenantMemberships` joined to `Users`. The `tenancy-guard.sh` hook warns on direct `Users` queries for exactly this reason.
- A tenant must always retain at least one active Owner — enforced on every membership change path.

## What a service may trust

| Source | Trust | Why |
| --- | --- | --- |
| `X-Actor-Id` header | ✅ | Gateway minted it after validating the cookie |
| `X-Tenant-Id` header | ✅ | Gateway resolved and checked membership |
| Event envelope actor | ✅ | Stamped by business at publish time |
| Anything in the request body | ❌ | Client-supplied |
| Anything in a query string | ❌ | Client-supplied |

**An actor id from a request body is the classic BOLA/IDOR shape.** Business layers take the actor from the ambient context, never from the payload.

## The weakness

Headers are not self-verifying. A service reachable off-gateway trusts whatever it's told — see [API Gateway](./api-gateway-edge-and-tenant-resolution.md). And one cookie covers every tenant a user belongs to, so a stolen session spans all of them.

A signed internal JWT minted per request by the gateway would fix the first and is the most likely successor to both ADR-0011 and ADR-0021.

## Standing rules

- The browser gets a cookie; services get headers. Never pass the cookie through.
- Actor comes from the ambient context, never the body.
- Role is per-tenant; there is no global admin flag.
- Never query `db.Users` for a tenant's people.
- Secrets stay out of source — user-secrets in dev, environment variables in production.
