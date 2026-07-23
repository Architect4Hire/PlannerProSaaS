# Tenancy SCRUB Prompts

*Focused prompts for the isolation mechanism itself — building it, extending it, and auditing it. The same SCRUB skeleton and golden rule as the [main prompt library](./scrub-prompts.md), fixed by ADR-0008, ADR-0009, ADR-0010 and ADR-0011.*

These exist separately because tenancy is the one mechanism where a mistake is a **breach rather than a bug**, and where two of the load-bearing behaviours are **assumptions until proven**. The main library builds the system; this one is what you reach for when you're working on the thing that keeps tenants apart.

---

## The golden rule for everything in this file

**A leak found in one service is a mechanism failure.** Fix it in `PlannerPro.Shared` and re-run the suite everywhere. Patching the endpoint where you found it leaves the same hole in seven other services and gives you the feeling of having fixed something.

---

## Prompt T1 — Prove the filter mechanism before building on it

*Run this before Prompt 2 of the main library, or as its first step. It is the cheapest possible way to avoid a week of wasted work.*

```
SCOPE: Prove — with a test, not an argument — that an EF Core global query filter closing over an
INJECTED, SCOPED ITenantContext re-evaluates per request rather than capturing the first tenant the
compiled model ever saw. Build the smallest possible harness: one entity implementing ITenantScoped,
a base DbContext applying the filter by reflection, a scoped ITenantContext, and a test that resolves
the context twice for two different tenants and asserts each sees only its own rows.

CONSTRAINT: Test the configuration we will actually run — including DbContext pooling if we intend to
use it, and the Aspire Npgsql registration. A test that passes without pooling and fails with it has
told us nothing useful.

RESTRICTION: Do NOT proceed to build anything on this assumption until the test passes. Do NOT make
the test pass by disabling pooling or the model cache without telling me that's what you did — that
is a finding, not a fix. Do NOT assume the behaviour from documentation or from memory of another EF
version.

USAGE: Use plan mode.

BEHAVIOR: First write out, in prose, exactly what you expect: when the filter expression is compiled,
when it is evaluated, and what the model cache holds. Show me that before writing code — if the
prediction is wrong, that's the most valuable output of this prompt. Then implement, run, and report
the actual result. If it fails, STOP and tell me; do not work around it.
```

## Prompt T2 — Prove consumer scope from the envelope

*Run alongside Prompt 3 of the main library.*

```
SCOPE: Prove that a Service Bus consumer operates under the tenant carried on the event envelope, and
that the two silent failure modes are impossible. Build a harness with two tenants, one ITenantScoped
entity, and a consumer that writes to it. Assert: (1) a message for tenant A causes a write visible
only to tenant A; (2) a consumer invoked with no established scope CANNOT silently write unstamped or
cross-tenant rows; (3) a message arriving with no TenantId is dead-lettered, not processed.

CONSTRAINT: Scope establishment must live in the processor host, be unavoidable, and require nothing
from the consumer author. Follow .claude/rules/messaging.md and .claude/rules/tenancy.md.

RESTRICTION: Do NOT let a consumer resolve its own tenancy from the payload body. Do NOT default
BypassFilters to true for background work. Do NOT make the "no TenantId" case a warning that then
processes anyway — dead-letter it.

USAGE: Use plan mode. Delegate review to tenant-isolation-auditor.

BEHAVIOR: Show me the scope-establishment sequence and explain how a consumer author is structurally
prevented from bypassing it. Wait for approval. Implement, test all three assertions, and report. Say
explicitly whether failure mode A (silent no-op) would be detectable in production logs — if not,
propose what would make it detectable.
```

## Prompt T3 — Add a tenant-scoped entity safely

```
SCOPE: Add <entity> to the <service> service: <fields, relationships, uniqueness>.

CONSTRAINT: Follow .claude/rules/tenancy.md. The entity implements ITenantScoped. EVERY uniqueness
constraint is tenant-scoped — (TenantId, X), never (X) alone. Filters must be applied automatically
by the base context, not by hand.

RESTRICTION: Do NOT create a globally unique index on tenant-scoped data — it leaks the existence of
other tenants' rows through constraint violations, and it is the rule most often missed. Do NOT
hand-write a HasQueryFilter call; if you find you need to, the mechanism has a gap and THAT is what to
fix. Do NOT apply the migration before I've seen the diff.

USAGE: Use the add-tenant-scoped-entity skill.

BEHAVIOR: Show me the entity, its indexes, and the intended migration name before generating. Wait.
Generate, show me the migration diff, wait again, then apply. Confirm the reflection test now covers
the new type — actually check that it enumerates it, don't assume. Add the cross-tenant test for
whatever surface exposes it. Run tenant-isolation-auditor.
```

## Prompt T4 — Build the cross-tenant isolation suite

*This is Prompt 8 of the main library, reproduced here because it is the gate everything else depends on.*

```
SCOPE: Build the cross-tenant integration suite every service must pass. Provision two tenants with
deliberately similar-looking data, then assert for EVERY tenant-scoped read and write: a request from
tenant B using tenant A's ids returns 404 — not 403, not 200 with partial data, not a validation error
naming the row — and mutates nothing. Add the reflection test enumerating every ITenantScoped type and
asserting a filter exists. Add a check that fails the build if FindAsync appears on a tenant-scoped
entity.

CONSTRAINT: Exercise the real stack including gateway header projection. The failure modes here live
in the seams, so unit-level mocks will pass while the system leaks.

RESTRICTION: Do NOT weaken an assertion to make a test pass. Do NOT let the reflection test pass
vacuously — prove it actually enumerates types by temporarily removing a filter and confirming it
fails. If a real leak is found, STOP and report it; fix the mechanism in Shared, not the endpoint.

USAGE: Use plan mode. Delegate to test-gap-analyzer and tenant-isolation-auditor.

BEHAVIOR: Plan the harness and the case matrix before writing. Wait for approval. Implement, run, and
report honestly — including anything that fails. Then tell me plainly whether we should proceed to the
fan-out. This suite is the gate.
```

## Prompt T5 — Audit an existing surface

```
SCOPE: Audit <the change / this service / the whole system> for cross-tenant leakage.

CONSTRAINT: Check all five layers, not just the obvious one: query filters, the SaveChanges
interceptor, gateway resolution and header stripping, role filters, and consumer envelope scope. Grep
for the known vectors: FindAsync, IgnoreQueryFilters, unique indexes without TenantId, direct db.Users
queries, cache keys without a tenant segment, unprefixed blob names, raw SQL against scoped tables.

RESTRICTION: This is READ-ONLY — report, do not fix. Do NOT downgrade a finding because it looks hard
to exploit; it either is possible or it isn't. Do NOT skip the bus path — consumers are where scope is
most often lost and least often reviewed.

USAGE: Delegate to tenant-isolation-auditor; also run test-gap-analyzer for missing isolation tests.

BEHAVIOR: Report findings ranked by severity, each with the file, the specific path a caller from
another tenant would take, and the concrete fix. Then list explicitly, layer by layer, what you
checked and found clean — a bare "no issues" is indistinguishable from not having looked. Then wait.
```

## Prompt T6 — Investigate a suspected leak

*If you're running this against a real report, treat it as an incident.*

```
SCOPE: A user reports seeing data that does not belong to their tenant: <what they saw, where, when,
which tenant they were in>.

CONSTRAINT: Diagnose before changing anything. Establish the path first: was it an HTTP request or
something rendered from a consumer-fed read model? That single distinction eliminates half the
possible causes.

RESTRICTION: Do NOT change code until you can state the root cause and name which of the five layers
failed. Do NOT "fix" it by widening a filter, adding IgnoreQueryFilters, or special-casing the
endpoint. Do NOT close the investigation at the first plausible cause — check whether the same
mechanism failure exists in the other seven services.

USAGE: Use the trace-a-request skill to reconstruct what actually happened from the audit trail.
Delegate to tenant-isolation-auditor.

BEHAVIOR: Report the root cause, which layer failed, the blast radius (which other services share the
same mechanism), and the smallest correct fix — in Shared if the mechanism failed. Add the regression
test that would have caught it. Tell me what data was exposed and to whom, as precisely as the trail
allows; that question will be asked and a vague answer is worse than none.
```

---

## Pro tips

- **The tenancy question, every time:** "Where does `TenantId` come from on this path?" Two right answers, gateway header or event envelope. Any third answer is a bug.
- **"No data" is usually a scope problem, not a query problem.** It reads like a query bug and gets debugged in the wrong place for an hour.
- **Two tenants in dev, always.** One tenant proves nothing; two make a leak visible by eye.
- **Re-run T1 on every EF upgrade.** The assumption it proves is version-sensitive, and nothing will tell you when it stops holding.
