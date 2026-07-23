# Exception Handling & Error Shape

**Decided by:** [ADR-0005](../../adr/0005-thin-host-core-layered-library.md) (mechanism lives in `Shared`)
**Rules:** [`.claude/rules/backend.md`](../../../.claude/rules/backend.md)

---

## One handler, one shape

A global exception handler in `PlannerPro.Shared` maps failures onto a single error shape, registered by every host. Controllers don't catch; layers throw and let the handler decide the status.

| Thrown | Becomes |
| --- | --- |
| `ValidationException` (FluentValidation, at the facade) | `400` with field-level detail |
| Domain exception | `4xx` per its kind |
| Limit exceeded | `402` with a machine-readable `limit` code |
| Not found **or wrong tenant** | `404` — identical shape for both |
| `CrossTenantWriteException` | `500` — this is a **defect**, not a client error |
| Anything else | `500`, logged, details not disclosed |

## Two entries that need explaining

**`CrossTenantWriteException` is not a client error.** It means the `SaveChanges` interceptor caught an attempt to modify another tenant's row — which means a query filter was bypassed somewhere upstream. It should be unreachable. Treat every occurrence as a bug to investigate, alert on it, and never map it to a tidy `403` that makes it look handled.

**Not-found and wrong-tenant must be indistinguishable.** Same status, same body, same message, broadly the same timing. If a wrong-tenant id produces a different error than a nonexistent one, the error shape has become the leak — see [Tenant Isolation](./tenant-isolation-defense-in-depth.md).

## What must never reach the client

- Stack traces, SQL, EF entity names, or internal field names.
- Anything confirming the existence of another tenant's data — including a validation message that names a real row.
- Which of "user not found" versus "wrong password" failed at login.
- Secrets, obviously, including in a logged exception message.

## Where validation happens

At the **facade**, with FluentValidation, before business runs. A validator that queries the database to check a reference is querying through a tenant filter — which is correct, and worth knowing, because a "reference not found" from another tenant is indistinguishable from one that never existed. That's the desired behaviour, not a bug.

## Consumers

A consumer that throws does **not** complete the message; it will be redelivered, and the inbox makes that safe (ADR-0004). Persistent failures reach dead-letter. A message with no `TenantId` is dead-lettered deliberately rather than processed (ADR-0009).

Don't swallow exceptions in a consumer to "keep the queue moving" — that converts a loud failure into a silent data gap.

## Standing rules

- Throw from layers; let the shared handler map.
- One error shape across every service.
- Not-found and wrong-tenant are identical to the client.
- `CrossTenantWriteException` is a defect — investigate, don't tidy.
- Never leak internal detail, and never distinguish auth failure modes.
