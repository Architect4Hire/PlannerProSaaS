# Layered Service Architecture

**Decided by:** [ADR-0005](../../adr/0005-thin-host-core-layered-library.md)
**Rules:** [`.claude/rules/backend.md`](../../../.claude/rules/backend.md) · **Skill:** `add-endpoint`

---

## The shape

Every service is **two projects**:

- **`PlannerPro.<Service>`** (host) — `Controllers/`, `Consumers/`, `Program.cs`. Entry points and composition root. Nothing else.
- **`PlannerPro.<Service>.Core`** (library) — the whole stack, plus models, validators, mappers. EF Core lives here.

Both build on `PlannerPro.Shared`. References are **one-way and acyclic**: `Contracts` ← `Shared` ← `<Service>.Core` ← `<Service>` ← `AppHost`. A sideways reference between services is a compile error, not a review comment.

## The layers and their strict responsibilities

| Layer | Does | Must not |
| --- | --- | --- |
| **Controller / Consumer** | Bind a ViewModel (or receive an event), call the facade, return `ActionResult<ServiceModel>` | Contain logic, touch EF, resolve tenancy |
| **Facade** | Validate the ViewModel, own ServiceModel caching, check the local quota snapshot | Map, touch EF, publish |
| **Business** | ViewModel → Domain, apply rules, **build** the integration event with its full envelope, Domain → ServiceModel | Validate, cache, touch EF, send |
| **Data layer** | Compose repository calls into a whole operation, enqueue the outbox row **in the same transaction** | Apply rules, map, cache, hold a `DbContext` |
| **Repository** | EF queries, `ExecuteInTransactionAsync` from the base repo | Anything else |

The rule of thumb when unsure: **if it decides, it's business; if it validates, it's facade; if it composes, it's the data layer; if it queries, it's the repository.**

## Three model types

| Type | Folder | Travels | Created by |
| --- | --- | --- | --- |
| **ViewModel** | `Managers/Models/ViewModels/` | client → controller → facade | Model binder |
| **Domain** | `Managers/Models/Domain/` | business ↔ data ↔ EF | Business (from VM) / EF (on load) |
| **ServiceModel** | `Managers/Models/ServiceModels/` | business → facade → controller → client | Business (from entity) |
| **IntegrationEvent** | `PlannerPro.Contracts/` | service → outbox → bus | Business |

There is no separate DTO layer — the Domain entity *is* the internal shape. Nothing leaks: no EF entity reaches a controller, no ViewModel reaches the database, and **no Domain entity crosses a service boundary** (that's what events are for).

**A ViewModel never carries `TenantId`.** Tenant identity is ambient (header or envelope) and stamped by the interceptor. A ViewModel with a tenant id is a security bug, not a convenience — see [Tenant Isolation](./tenant-isolation-defense-in-depth.md).

## Registration

`.Core` owns its own wiring via `Add<Service>Core()` — every layer plus validators from its own assembly. `Program.cs` calls that plus the Shared extensions. No per-layer registration scattered through the host.

## The honest trade

This is more ceremony than a two-field lookup deserves; a simple read still travels through four layers. The payoff is that eight services look identical, which is what makes an agent's output predictable and a reviewer's job small. It is a deliberate trade of per-change friction for cross-service consistency.

It is also **not tactical DDD** (ADR-0005). Aggregates and invariants are conventions here, not types. A domain with genuinely complex invariants would outgrow this shape — that would be the trigger to revisit, not a reason to bend the layers.

## Standing rules

- Host stays thin; `.Core` holds the stack.
- ViewModels in, ServiceModels out, never an EF entity at the boundary.
- Async throughout; no `.Result`, no `.Wait()`.
- `FirstOrDefaultAsync`, never `FindAsync`.
- One type per file, named for the type.
- If an operation is growing, extract a private helper before you reach for a new layer.
