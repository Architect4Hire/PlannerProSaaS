# Aspire Orchestration

**Decided by:** [ADR-0012](../../adr/0012-aspire-local-first-emulators.md)
**Rules:** [`.claude/rules/aspire.md`](../../../.claude/rules/aspire.md) · **Skill:** `add-aspire-resource`

---

## One file describes the system

`PlannerPro.AppHost/AppHost.cs` declares every resource: a Postgres server with eight databases, the Azure Service Bus emulator, Azurite, Redis, eight service hosts, the gateway, and the Angular app. `aspire run` starts all of it.

Nothing outside the AppHost invents infrastructure, and **nothing hardcodes an address**.

## Wire with the model, not with strings

```
WithReference(db)   → connection info injected as configuration
WaitFor(db)         → startup ordering
```

No connection strings, broker addresses, blob endpoints, or `localhost:port` in code. **One sanctioned exception:** the gateway's YARP clusters name destinations by Aspire resource name (`http://planning`), because service discovery resolves them — that's using the model, not bypassing it.

The `secret-guard.sh` hook treats any literal credential as a defect precisely because this rule leaves no legitimate reason for one.

## Local-first, and what that means

An **emulator-backed** Azure resource is in bounds because it is a local container: `AddAzureServiceBus(...).RunAsEmulator(...)`, `AddAzureStorage(...).RunAsEmulator(...)`. The test is *where it runs*, not what the API is called. A resource needing a real subscription — `AsExisting`, real provisioning — is out.

> ⚠️ **The emulator is not the broker.** Dead-lettering, retry semantics, and throughput differ. Anything depending on those specifics needs verification against the real Service Bus before it can be trusted (risk #13).

## ServiceDefaults

Cross-cutting configuration lives once: OpenTelemetry, health checks, resilience, service discovery. Every host and the gateway call `AddServiceDefaults()`.

**`TenantId` enrichment on spans and log scopes belongs here** — one place, so every service gets it. Debugging a multi-tenant system without a tenant tag on every span is guesswork.

## Adding a resource vs. adding a service

| Request | Verdict |
| --- | --- |
| A database, topic, container, cache | Wiring — proceed |
| A **new service** | Architectural decision — propose it, write an ADR, wait |

Eight was a decision (ADR-0001), not an accident.

## What's missing

**There is no production deployment story.** Local-first is a development decision and says nothing about production. Two requirements follow from decisions already made: services must be unreachable except via the gateway (risk #1), and emulator-versus-real behaviour must be verified.

## Standing rules

- Every resource is declared in the AppHost, with a comment explaining why.
- Wire with `WithReference` / `WaitFor`; never a literal address.
- One database per service; never two services on one database.
- Local containers only — nothing needing a real subscription.
- A new service needs an ADR, not just an AppHost line.
- Verify fast-moving Aspire API names against https://aspire.dev rather than trusting recall.
