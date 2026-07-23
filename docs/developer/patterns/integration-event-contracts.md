# Integration Event Contracts

**Decided by:** [ADR-0013](../../adr/0013-contracts-leaf-tenant-envelope.md), [ADR-0002](../../adr/0002-event-driven-integration-over-service-bus.md)
**Subagent:** `@api-contract-checker`

---

## The leaf property

`PlannerPro.Contracts` **references nothing**. Everything else may reference it. It sits at the bottom of the dependency graph, and that is what makes the service boundary real: a service's domain type cannot reach another service, because it cannot reach Contracts' consumers.

It holds integration-event records and the `IIntegrationEvent` marker. **No domain logic, no EF, no helpers, no DTOs that aren't events.** The moment a utility lands here, it starts attracting domain code and the boundary becomes decorative.

## The mandatory envelope

`IIntegrationEvent` requires:

| Field | Purpose |
| --- | --- |
| `Id` | Dedupe key for the inbox; `MessageId` on the wire |
| **`TenantId`** | The only way a consumer can establish scope |
| `CorrelationId` | Constant across a request's whole fan-out |
| `CausationId` | The `Id` of the event that directly caused this one |
| Actor | Who did it, projected by the edge |

**An event type that omits any of these does not compile.** The envelope is enforced by the type system, not by review — which matters because a missing `TenantId` is a silent cross-tenant failure ([Tenant Scope on the Bus](./tenant-scope-on-the-bus.md)).

## Event shape

- **Immutable records, past tense:** `ProjectCreated`, `InvitationAccepted`, `AttachmentUploaded`.
- **Minimal fields:** IDs plus the smallest amount of denormalized data that saves a consumer from calling back. `ProjectCreated` carries the name and colour because read models render them.
- **Enums cross as strings**, so adding a value doesn't shift the meaning of existing data.
- No behaviour, no EF types, no service's Domain types.

## Changing an event is a contract change

Every service compiles against every event. Changing one:

1. List every consumer (`@api-contract-checker` will do it).
2. Update them in the **same change**.
3. Adding an optional field is usually safe; removing or renaming one is not.

In a monorepo this is a compile error, which is the good version of this problem. If services ever version independently, this becomes a genuine versioning discipline — the first thing ADR-0013 would need revisiting for.

## The other contract surface

The same subagent checks **ServiceModel ↔ Angular model** drift: C# `PascalCase` → TS `camelCase`, `T?` → `T | null`, `Guid` → `string`, enums → string unions. The C# side wins.

One check worth calling out: **`TenantId` should never appear on a ViewModel or a TS request type.** If it does, that's a contract finding *and* a security finding.

## Standing rules

- Contracts references nothing; it is the leaf.
- Events are immutable, past-tense, minimal-field records.
- The full envelope is mandatory and compiler-enforced.
- Changing an event means updating every consumer in the same change.
- Enums cross as strings.
