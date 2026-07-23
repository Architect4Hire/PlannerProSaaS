# ADR-0013: `Contracts` as a leaf library with a mandatory tenant envelope

- **Status:** Accepted
- **Date:** 2026-07-23
- **Deciders:** Robert Felkins
- **Related:** ADR-0002 (events), ADR-0005 (references), ADR-0009 (bus scope), ADR-0015 (correlation/causation)

## Context

Integration events are the only thing that crosses a service boundary. Where their types live determines whether the boundary holds: if the shared contracts library can reference a service, it becomes a back door for shared domain code, and the services are coupled again through the type system.

Separately, ADR-0009 makes tenant scope structural rather than procedural — which only works if *every* event carries it, with no way to define one that doesn't.

## Decision

**`PlannerPro.Contracts` is a leaf library holding event records and nothing else, and `IIntegrationEvent` mandates the full envelope.**

- Contracts **references nothing**; everything else may reference it. It is the bottom of the dependency graph.
- It contains integration-event records and the `IIntegrationEvent` marker. No domain logic, no EF, no helpers, no DTOs that aren't events.
- `IIntegrationEvent` requires `Id`, **`TenantId`**, `CorrelationId`, `CausationId`, and actor. **An event type that omits any of these does not compile** — the envelope is enforced by the type system, not by review.
- Events are **immutable records, named in the past tense** (`ProjectCreated`, `InvitationAccepted`), carrying IDs plus the minimum denormalized data a consumer needs to avoid calling back.
- Enums cross the wire **as strings**, so adding a value doesn't shift the meaning of existing data.
- Changing an existing event is a **contract change** affecting every consumer; consumers are listed and updated in the same change.

## Consequences

**Positive**
- The boundary is enforced by the compiler: a service's domain type cannot reach another service, because it cannot reach Contracts' consumers.
- Tenant scope cannot be forgotten on a new event — the failure is a build error rather than a runtime leak.
- The envelope simultaneously satisfies isolation (ADR-0009) and the audit trail (ADR-0016) with one mechanism.

**Negative**
- Every service compiles against every event, so a Contracts change rebuilds everything. In a repo with independent deployment this becomes a versioning problem — acceptable in a monorepo, and the first thing to revisit if services ever version separately.
- Minimal-field events mean some consumers denormalize data they'd rather have looked up.
- Enum-as-string trades compile-time safety at the consumer for forward compatibility.

**Neutral**
- The envelope adds five fields to every message; negligible in bytes, meaningful in discipline.

## Alternatives considered

- **A shared "Common" library holding events and utilities.** Rejected: utilities attract domain code, and the leaf property is what makes the boundary real.
- **Per-service contract packages consumed as NuGet.** The correct answer for independently versioned services. Rejected as premature for a monorepo showcase, and it would slow the feedback loop considerably.
- **Schema-registry contracts (Avro/Protobuf).** Better evolution guarantees. Rejected as disproportionate; C# records with a compiler-enforced envelope get most of the benefit here.
- **Envelope as a base class with defaults.** Rejected: a default value for `TenantId` is exactly the silent failure ADR-0009 exists to prevent.
