# Adding an Endpoint by Hand

*The full slice, without Claude Code. This is what the [`add-endpoint`](../../.claude/skills/add-endpoint/SKILL.md) skill automates — worth doing once by hand so you know what the skill is doing and can tell when it's wrong.*

**Related:** [Layered Service Architecture](./patterns/layered-service-architecture.md) · [Tenant Isolation](./patterns/tenant-isolation-defense-in-depth.md) · [ADR-0005](../adr/0005-thin-host-core-layered-library.md)

---

## Step 0 — Decide which service owns it

The owner is the service whose **database** holds the data being changed or read. A sprint goal lives in Planning; a client in Portfolio; a membership in Access; a limit in Billing.

**If the work reads or writes two services' data, it is not one endpoint.** It's an endpoint in one service plus an integration event the other reacts to. Never open a second database. If ownership is unclear, that's a design conversation, not a coding decision.

## Step 1 — Write down the tenancy story

Before any code. Four questions, answered out loud:

1. **Where does `TenantId` come from?** Gateway header (HTTP) or event envelope (bus). There is no third answer.
2. **Which entities are `ITenantScoped`?** Are any deliberately not, and why?
3. **What does a caller from another tenant see?** A plain 404 — same shape, same message as a nonexistent resource.
4. **What role does this require?** Viewer reads; Member edits goals and tasks; Admin manages clients, teams, members, settings and branding; Owner changes plan, deletes the tenant, transfers ownership.

If you skip this step you will write a working endpoint with a leak in it, and the leak will be found by someone else.

## Step 2 — The models

In `PlannerPro.<Service>.Core/Managers/Models/`:

- **`ViewModels/`** — the inbound request type. Positional record. **Never carries `TenantId`.**
- **`ServiceModels/`** — the outbound response type. The only thing that leaves.
- **`Domain/`** — the EF entity, implementing `ITenantScoped`.

Add a FluentValidation validator in `Managers/Validators/` for the ViewModel, and mappers in `Managers/Mappers/`.

## Step 3 — Repository

`Data/I<Feature>Repository` + implementation. EF queries only.

**`FirstOrDefaultAsync`, never `FindAsync`.** `Find` returns tracked entities *without applying the tenant query filter* — the single most likely way this system leaks. The `tenancy-guard.sh` hook warns on it; don't rely on the warning.

You never write `WHERE TenantId = …`. The filter is applied automatically by the base context.

## Step 4 — Data layer

`Data/I<Feature>DataLayer` + implementation. Composes repository calls into a whole operation and — if this change publishes an event — enqueues the outbox row **inside the same transaction**, via `ExecuteInTransactionAsync`:

```
ExecuteInTransactionAsync(async () => {
    await repository.Add(entity);
    await outbox.Enqueue(integrationEvent);
});
```

You don't set `TenantId` on the entity. The `TenantSaveChangesInterceptor` stamps it from the ambient context.

## Step 5 — Business

`Business/I<Feature>Business` + implementation. Maps ViewModel → Domain, applies domain rules, **builds** the integration event with its full envelope (`Id`, `TenantId`, `CorrelationId`, `CausationId`, actor — actor from the ambient context, **never** from the payload), maps Domain → ServiceModel.

No validation here, no caching, no EF, no sending.

## Step 6 — Facade

`Facade/I<Feature>Facade` + implementation. Validates the ViewModel, owns ServiceModel caching (**cache key must include `TenantId`**), and — if this operation is limited by plan — checks the **local quota snapshot** and returns `402` with a machine-readable code. Never calls Billing synchronously.

## Step 7 — Controller

In the host, `Controllers/<Feature>Controller.cs`. Bind the ViewModel, apply `RequireTenantRole(...)`, call the facade, return `ActionResult<ServiceModel>`. No logic.

If this is event-driven instead, write a `Consumers/<Event>Consumer.cs` implementing `IIntegrationEventConsumer<TEvent>` that calls the **same facade**, dedupes via the inbox, and contains **no tenancy code** — the processor host has already established scope.

## Step 8 — Contract (if publishing)

Add the past-tense record to `PlannerPro.Contracts`. The envelope is compiler-enforced, so you can't omit `TenantId`. Changing an existing event means updating every consumer in the same change.

## Step 9 — Wiring

Register the new layers in `Add<Service>Core()`. Register a consumer in the host.

## Step 10 — Gateway route

A client-facing endpoint needs a route in `PlannerPro.Gateway`, named by Aspire resource name. No route means unreachable — which is correct for internal-only endpoints (Notifications has none).

## Step 11 — Migration (if the model changed)

From the **host** folder (the `DbContext` is in `.Core`, but DI and config resolve in the host):

```
dotnet ef migrations add <Name> \
  --project ../PlannerPro.<Service>.Core \
  --startup-project . \
  --context <Service>DbContext
```

Review it. Confirm every new unique index includes `TenantId`. Don't hand-edit beyond review. Commit it with the entity change.

## Step 12 — Tests

- Facade: validation failure, cache hit and miss, quota refusal.
- Business: the domain rule, and that the event carries the full envelope.
- Data layer: **rollback leaves neither the domain row nor the outbox row.**
- Consumer: redelivery applies once; scope comes from the envelope.
- **Cross-tenant: a caller from tenant B using tenant A's ids gets 404 and mutates nothing.**

That last one is not optional. A new tenant-scoped surface without a cross-tenant test is a gap the `test-gap-analyzer` will rank at the top.

## Step 13 — Review

Run `@code-reviewer` and `@tenant-isolation-auditor`. The second is the one that matters here.

---

## Checklist

- [ ] Correct owning service; no second database, no synchronous cross-service call
- [ ] Tenancy story written down before coding
- [ ] `TenantId` on no ViewModel; never read from body, query or route
- [ ] **No `FindAsync`**; no `IgnoreQueryFilters` outside a bypass context
- [ ] Cache key includes `TenantId`
- [ ] Event published through the outbox in the same transaction, full envelope
- [ ] Consumer idempotent, no tenancy code
- [ ] Role filter applied; wrong tenant and not-found are indistinguishable
- [ ] Unique indexes are `(TenantId, …)`
- [ ] Gateway route added, or deliberately omitted
- [ ] Cross-tenant test written and passing
