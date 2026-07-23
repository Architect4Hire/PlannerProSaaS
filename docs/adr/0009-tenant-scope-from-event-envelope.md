# ADR-0009: Tenant scope for consumers is established from the event envelope

- **Status:** Accepted
- **Date:** 2026-07-23
- **Deciders:** Robert Felkins
- **Related:** ADR-0002 (events), ADR-0004 (inbox), ADR-0008 (discriminator & filters), ADR-0011 (gateway projection), ADR-0013 (contracts)

## Context

This is the decision that has no counterpart in the single-tenant design, and the one most likely to be got wrong.

Query filters (ADR-0008) close over a **scoped** `ITenantContext`, populated by middleware from the gateway's projected headers (ADR-0011). That works for every HTTP request. **A Service Bus consumer is not an HTTP request.** It runs on a background thread, resolved from a scope the processor host creates, with no incoming headers and no ambient tenant.

If nothing establishes scope, one of two things happens, both silent: the context resolves to an unset tenant and every query returns nothing (the consumer appears to work and quietly does nothing), or `BypassFilters` is defaulted on and the consumer reads and writes **across every tenant in the database**.

There is no ambient source of truth on the bus. The only thing that knows which tenant a message belongs to is the message.

## Decision

**Every integration event carries `TenantId` in its envelope, and the Service Bus processor host establishes `ITenantContext` from it before invoking the consumer.**

- `IIntegrationEvent` requires `TenantId` alongside `Id`, `CorrelationId`, `CausationId`, and actor. An event type without one does not compile.
- The dispatcher promotes `TenantId` to a Service Bus application property so it is readable without deserializing the body.
- The `ServiceBusProcessorHost` opens a scope, populates `ITenantContext` from the envelope, and only then resolves and invokes the consumer. Scope establishment is **central and unavoidable** — a consumer author cannot forget it because a consumer author never does it.
- **A consumer must never resolve its own tenancy**, and must never read a tenant id from the message body in place of the envelope.
- **A message arriving with no `TenantId` is dead-lettered**, not processed under a guessed or default scope. Failing loudly beats writing to the wrong tenant.

## Consequences

**Positive**
- Consumers are written exactly like request-path code: they query and write, and the filter and interceptor do their work. No consumer contains tenancy plumbing.
- The failure mode is loud (dead-letter) rather than silent (wrong tenant).
- Because scope lives in the processor host, changing it is one edit rather than one per consumer.

**Negative**
- **This is the single most breakable mechanism in the system.** Every consumer in every service depends on it, and a regression here is a cross-tenant write with no obvious symptom. It warrants its own tests, its own subagent checks, and unusual care on any change.
- Every event pays the envelope cost, including ones whose consumers don't need the tenant.
- Platform-level events that legitimately span tenants don't fit the model and must run under an explicit bypass context — narrow, commented, and rare.

**Neutral**
- The envelope carries tenancy and causality together, so the same mechanism that makes consumers safe also feeds the audit trail (ADR-0015, ADR-0016).

## Alternatives considered

- **Let each consumer resolve its own tenant from the payload.** Rejected: correctness depending on eight services' worth of consumer authors remembering, with a silent failure mode. This is exactly the class of decision that should be structural, not procedural.
- **Run consumers with filters bypassed and scope queries manually.** Rejected: turns the strongest default defense off precisely where nobody is watching.
- **A topic (or subscription) per tenant.** Real isolation on the transport, and worth revisiting for a small number of very large tenants. Rejected here: topic count grows with the customer list, and provisioning becomes part of signup.
- **Derive tenant from the message's originating service.** Meaningless — every service serves every tenant.
