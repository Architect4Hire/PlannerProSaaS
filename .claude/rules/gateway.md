---
paths:
  - src/PlannerPro.Gateway/**
---
# Gateway rules — YARP reverse proxy + tenancy resolution

`PlannerPro.Gateway` is the **only** public entry point and the **only** place tenancy is resolved.
The Angular app talks to the gateway and nothing else; services are never exposed to the browser.
Keep it declarative — routing, tenancy resolution, and edge cross-cutting only. No business logic.

- **Route by Aspire resource name, not by address.** YARP `Clusters` name destinations by service
  discovery name (e.g. `http://planning`, `http://portfolio`), which Aspire resolves. Never a literal
  host:port. This is the one sanctioned place a service name appears in config, because discovery
  reads it.
- **Resolve tenancy once, here.** For `/api/t/{slug}/…` routes: slug → tenant (memory-cached, short
  TTL) → the caller's `TenantMembership`. Then **strip** every client-supplied tenant, actor and
  correlation header and project the gateway's own trusted set inward (tenant id, slug, role, tenant
  status, actor id, correlation id), echoing the correlation id on the response. A service never
  re-resolves a slug and never trusts a header the gateway didn't mint.
- **404, never 403, for a non-member.** Never confirm a tenant exists to someone outside it — not via
  status code, not via message, not via a distinguishable response shape or timing on an obvious path.
- **Suspended means read-only.** Tenants whose status is `Suspended`, `PastDue` or `Cancelled` may
  read and export; writes are refused at the edge. Suspension is never implemented as a login block.
- **Some routes deliberately skip tenant resolution:** `/api/ping`, `/api/public/*` (including the
  anonymous branding endpoint), `/api/auth/*`, `/api/signup`, `/api/invitations/*`, `/api/me/tenants`,
  and `/api/admin/*` (platform admin, gated on `IsPlatformAdmin`). Everything else is tenant-scoped.
- **Every client-facing endpoint needs a route.** A service endpoint with no gateway route is
  internal-only by design (fine — Notifications has none). A client-facing endpoint without one is
  unreachable; adding the route is the last step of shipping it.
- **Auth at the edge.** Credentials are validated here before proxying. Keep signing keys and secrets
  out of source — wire via config/Aspire.
- **Edge cross-cutting only.** Rate limiting (including **per-tenant** limits), CORS for the Angular
  origin, and trace propagation belong here; anything domain-specific belongs in a service. The
  gateway calls `AddServiceDefaults()` so its traces join the rest.
- **Stable public contract.** The gateway's public paths are what the frontend depends on; the
  service boundaries behind them can move without the client noticing. Don't reshape a public path
  casually.

Verify YARP-with-Aspire wiring (transforms, service-discovery destination syntax, package names)
against https://aspire.dev and the YARP docs — these move between versions.
