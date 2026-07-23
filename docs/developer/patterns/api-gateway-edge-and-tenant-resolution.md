# API Gateway, Edge & Tenant Resolution

**Decided by:** [ADR-0006](../../adr/0006-single-api-gateway-yarp.md), [ADR-0007](../../adr/0007-path-based-tenancy-slug-routing.md), [ADR-0011](../../adr/0011-gateway-tenant-resolution-header-projection.md), [ADR-0020](../../adr/0020-suspended-tenants-read-only.md)
**Rules:** [`.claude/rules/gateway.md`](../../../.claude/rules/gateway.md)

---

## Two jobs, one component

`PlannerPro.Gateway` (YARP) is the **only public door** and the **only place tenancy is resolved**. Both properties matter: the first gives one place to enforce edge concerns, the second means eight services don't each implement the most security-sensitive check in the product.

## What happens to a request

```
GET /api/t/acme/sprints/current
    ↓
1. Validate the cookie (ADR-0021)
2. STRIP any client-supplied tenant / actor / role / correlation headers
3. Resolve slug "acme" → tenant   (memory-cached, short TTL)
4. Resolve the caller's TenantMembership
   └─ no active membership? → 404. Never 403.
5. Tenant status Suspended/PastDue/Cancelled?
   └─ non-GET? → refuse (read-only mode)
6. PROJECT trusted headers inward:
     X-Tenant-Id, X-Tenant-Slug, X-Tenant-Role, X-Tenant-Status,
     X-Actor-Id, X-Correlation-Id
7. Route by Aspire resource name → http://planning
8. Echo X-Correlation-Id on the response
```

Steps 2 and 6 are a pair. Stripping without projecting breaks the app; projecting without stripping means a client can assert its own tenancy. **Never separate them.**

## 404, never 403

A caller who isn't a member of `acme` gets a plain 404 — the same status, the same response shape, the same message, and broadly the same timing as a slug that doesn't exist. A 403 confirms the tenant is real, which is information a non-member shouldn't have.

The same applies to a resource id belonging to another tenant, and to a validation error that names an internal field. **The message is the leak, not just the status code.**

## Routes that skip tenant resolution

Deliberately outside the tenant segment: `/api/ping`, `/api/public/*` (including anonymous branding), `/api/auth/*`, `/api/signup`, `/api/invitations/*`, `/api/me/tenants`, `/api/admin/*` (platform admin). Everything else is tenant-scoped.

## The exposure this creates

**Every service unconditionally trusts these headers.** If a service is reachable without traversing the gateway, tenant isolation is not degraded — it is gone. Network-level enforcement that services accept traffic only from the gateway is therefore a **hard deployment requirement**, and it is currently **unimplemented** (risk #1 in the [register](../../design/build-plan-and-risks.md)).

The likeliest fix is superseding ADR-0011 and ADR-0021 together with a signed internal token, which would be self-verifying and remove the network dependency.

## Cache invalidation

Membership is cached at the edge. A revoked member or a changed role stays effective for the TTL. Invalidate on membership change, keep the TTL short, and **state the window** rather than pretending it's zero.

## Standing rules

- Route by Aspire resource name; never a literal host:port.
- Strip client headers, project your own. Always both.
- 404, never 403 — same shape, same message.
- Suspended means read-only, never a login block.
- Edge cross-cutting only. No business logic in the gateway.
- A service never re-resolves a slug.
