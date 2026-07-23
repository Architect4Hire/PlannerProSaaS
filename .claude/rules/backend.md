---
paths:
  - src/PlannerPro.*/**
  - "!src/PlannerPro.AppHost/**"
  - "!src/PlannerPro.ServiceDefaults/**"
  - "!src/PlannerPro.Gateway/**"
  - "!src/PlannerPro.Contracts/**"
---
# Backend rules — ASP.NET Core + EF Core (Aspire microservices)

Every service is **two projects**: a thin host (`PlannerPro.<Service>`) and a class library
(`PlannerPro.<Service>.Core`). Both build on `PlannerPro.Shared`. The full playbook for adding a route
or consumer is the `add-endpoint` skill; these are the standing rules.

- **The host is thin.** `PlannerPro.<Service>` holds only entry points — `Controllers/`, `Consumers/`
  — and the composition root (`Program.cs`). No business logic, EF, or data access in the host.
- **`.Core` holds the stack.** Facade → business → data layer → repository, plus models, validators,
  and mappers, all live in `PlannerPro.<Service>.Core`. EF Core / SQL Server lives here, never in the host.
- **The layers, and their strict responsibilities:**
  - **Controller / Consumer** — bind a ViewModel (or map an integration event) and call the facade;
    return an `ActionResult<ServiceModel>`. A consumer is idempotent (inbox) and receives its tenant
    scope from the processor host. No logic.
  - **Facade** — validates the ViewModel and owns caching of ServiceModels. No mapping, EF, or bus.
  - **Business** — translates ViewModel → Domain, applies domain rules, *builds* the integration
    event when a change warrants one, maps Domain → ServiceModel. No validation, cache, EF, or send.
  - **Data layer** — composes repository calls into whole operations and enqueues the outbox row in
    the same transaction via `IOutbox`. No rules, mapping, cache, validation, or `DbContext`.
  - **Repository** — EF queries against `<Service>DbContext`, plus `ExecuteInTransactionAsync` from
    the Shared base repository. Data only.
- **Three model types at the boundary.** Only **ViewModels** enter and only **ServiceModels** leave;
  **Domain** entities are the internal shape. Never expose an EF entity across the controller, and
  never let a Domain entity cross the *service* boundary — that's what integration events are for.
- **`TenantId` is never a ViewModel field.** It arrives ambiently (header or envelope) and is stamped
  by the interceptor. A ViewModel carrying one is a security bug — see `.claude/rules/tenancy.md`.
- **DbContext via Aspire.** `<Service>DbContext` (in `.Core`, deriving from the Shared base context)
  is registered through the Aspire SQL Server integration keyed to the service's database resource (e.g.
  `planningdb`), not by reading a raw connection string.
- **Registration is owned by `.Core`.** Expose `Add<Service>Core()` from the library (registers every
  layer + validators from its own assembly); the host's `Program.cs` calls it plus the Shared
  extensions. No per-layer wiring scattered through the host.
- **Async all the way.** `async Task<...>` with `await`; never `.Result` or `.Wait()`.
- **Never `FindAsync`.** It bypasses query filters on tracked entities. `FirstOrDefaultAsync`, always.
- **Validate at the edge.** FluentValidation in `.Core/Managers/Validators/`; on failure the shared
  exception handler returns the shared error shape, not a raw exception.
- **Service defaults.** Every host's `Program.cs` calls `AddServiceDefaults()`.
- **EF Core workflow (split project).** The `DbContext` is in `.Core`; the host is the startup
  project. Run from the host folder:
  `dotnet ef migrations add <n> --project ../PlannerPro.<Service>.Core --startup-project . --context <Service>DbContext`,
  review, then `database update`. Commit the migration.
- **Naming.** PascalCase types/methods, `_camelCase` private fields, camelCase locals.
- **One type per file,** named for the type — records, enums, and interfaces included. The only
  companions allowed to share a file are ones with no meaning apart from their parent.

Domain lives in each service's `.Core`: **Access** — `Tenant`, `TenantSettings`, `TenantBranding`,
`TenantMembership`, `Invitation` (+ global Identity); **Portfolio** — `Client`, `Team`, `TeamMember`,
`Project`; **Planning** — `Sprint`, `SprintGoal`, `PlannerTask`, `SprintCapacity`; **Roadmap** —
`RoadmapGoal`; **Files** — `Attachment`, `TenantAsset`; **Billing** — `Plan`, `TenantSubscription`,
usage counters; **Audit** — `AuditEntry`.
