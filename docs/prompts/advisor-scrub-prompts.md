# Sprint Advisor — SCRUB Prompts

*Prompts for building the AI capability: a ninth bounded context (`PlannerPro.Advisor`) that produces a pre-sprint risk brief grounded in each tenant's own history, using **Semantic Kernel** for orchestration, **OpenAI** for inference, and **Azure AI Foundry** for prompt versioning, evaluation, and content safety.*

Same SCRUB skeleton and the same golden rule as the [main library](./scrub-prompts.md). Run Part 1 in order.

---

## Why this sequence looks the way it does

**The golden rule here: build the deterministic half first.** Prompts A1 and A2 stand up the service, its read model, and a rules-based risk score with **no model call at all**. Only A3 introduces inference. That ordering is deliberate — a rules-based baseline is the thing you evaluate the model *against*, and without it "the AI seems good" is the only quality signal you will ever have.

**The second rule: retrieval before generation.** A4 builds tenant-partitioned retrieval and proves isolation before any retrieved content reaches a prompt. Context assembly is the leak surface here, and it is the one your existing five layers do not cover.

## What makes this different from every other feature in this repo

| Concern | Everywhere else | Here |
| --- | --- | --- |
| Leak surface | A row crossing a filter | **Tenant A's history appearing as prose inside tenant B's brief** |
| Isolation mechanism | EF query filter | **Index-level partitioning** — vector search never touches a query filter |
| Idempotency | Don't apply the effect twice | Don't apply it twice **and don't re-spend tokens** |
| Correctness | Deterministic; test asserts equality | Non-deterministic; test asserts **bounds and behaviour** |
| Failure | 500, retry | Plausible, confident, wrong — **and no exception** |
| Local dev | Emulator container | **No emulator exists.** Requires a deterministic stub provider |

---

# Part 1 — Building the Advisor (run once, in order)

## Prompt A0 — Write the ADRs, and wait

*The constitution says a new service is an architectural decision, not a feature. This prompt exists to honor that rather than route around it.*

```
SCOPE: Do not write any implementation code. Draft the Architecture Decision Records required before
PlannerPro can gain an AI capability, following the exact format in docs/adr/README.md and numbering
from 0022:

  ADR-0022  The Advisor bounded context — a ninth service, consumer-fed, advisory-only
  ADR-0023  Semantic Kernel as the orchestration layer; OpenAI for inference; Foundry for
            evaluation, prompt versioning and content safety
  ADR-0024  Tenant-partitioned retrieval — pgvector in advisordb, isolation enforced in the index
  ADR-0025  AI execution is asynchronous, event-driven, and cost-idempotent
  ADR-0026  AI credits as a plan limit (extends ADR-0017's replicated-quota pattern)
  ADR-0027  Amendment to ADR-0012 — the local-first exception and the deterministic stub provider

Each must state real alternatives and real consequences, not a rationalization of a decision already
made.

CONSTRAINT: Follow the established ADR shape: Context / Decision / Consequences (Positive, Negative,
Neutral) / Alternatives considered. Relate each to the ADRs it depends on or amends. Reuse existing
patterns where they fit — ADR-0026 in particular should be short, because replicated quotas already
solve it.

RESTRICTION: Do NOT understate the negatives. Specifically, ADR-0022 must state that the Advisor is
ADVISORY ONLY and never mutates another service's data, and say what that forecloses. ADR-0024 must
state plainly that vector similarity search does NOT pass through an EF query filter, so isolation is
enforced somewhere new — this is the sharpest edge of the whole feature. ADR-0025 must address what a
redelivered event costs in tokens. ADR-0027 must be honest that "local-first, zero cloud spend" is
being amended, not preserved, and say what a contributor without an API key actually gets. Do NOT
write an ADR that concludes "and there are no real downsides."

USAGE: Read docs/adr/README.md, ADR-0001, ADR-0012, ADR-0016 and ADR-0017 first — 0016 is the closest
precedent for proposing a new consumer-fed service, and 0017 for the quota pattern.

BEHAVIOR: Draft all six and show them to me. Then STOP. Do not scaffold anything. I want to approve
the decisions before any code exists, and I specifically want to reconsider whether this should be a
ninth service or a capability inside Planning — argue both sides of that in ADR-0022 rather than
assuming the answer.
```

## Prompt A1 — The Advisor service and its read model (no AI)

```
SCOPE: Build PlannerPro.Advisor + PlannerPro.Advisor.Core over advisordb, following the established
service pattern exactly. It is consumer-fed and has one read-only query surface. Build the read model
ONLY — no model calls, no Semantic Kernel, no retrieval.

Consume and maintain, as tenant-scoped local read models:
  - SprintHistory       — from SprintGoalSet, TaskChanged, sprint close events: per (sprint, project)
                          the goal text, final status, planned points, completed points, slip flag
  - EstimateOutcome     — per task: estimated points vs. actual effort signal, for calibration
  - AllocationSnapshot  — from CapacitySet and team membership: who is on which project, at what share
  - ProjectReference    — name, client, colour (the same pattern Planning already uses)

Expose GET /api/t/{tenant}/advisor/history as a read-only diagnostic surface so the read model can be
inspected before anything depends on it.

CONSTRAINT: Follow .claude/rules/backend.md, .claude/rules/tenancy.md and .claude/rules/messaging.md.
Thin host + .Core stack. Every entity implements ITenantScoped. Consumers are inbox-idempotent and
take scope from the envelope. Register advisordb in the AppHost with WithReference/WaitFor.

RESTRICTION: Do NOT call any model. Do NOT add Semantic Kernel, an AI package, or pgvector yet. Do
NOT read another service's database — everything arrives by event. Do NOT let the Advisor write to
any store but advisordb, now or ever. Do NOT use FindAsync.

USAGE: Use the add-endpoint and add-tenant-scoped-entity skills. Use plan mode. Delegate review to
code-reviewer, test-gap-analyzer and tenant-isolation-auditor.

BEHAVIOR: Plan the read-model shape first — specifically, what "slipped" means as a derived fact and
which existing events actually carry enough to compute it. If an event does NOT carry what's needed,
say so rather than inferring it; that's a contract gap to fix in the publisher, not to paper over
here. Wait for approval. Then implement, migrate, test (including the cross-tenant suite), and report
which of the four read models are fully supported by today's events and which are not.
```

## Prompt A2 — The deterministic baseline (still no AI)

*The thing you will measure the model against. Skipping this means never knowing whether the AI helps.*

```
SCOPE: Implement a rules-based sprint risk score in Advisor.Core, with no model involvement. For each
(sprint, project) goal, produce a RiskAssessment: a score, a band (Low/Medium/High), and a list of
plain-language reasons, computed from the read model — historical slip rate for this project, planned
points versus the tenant's overload threshold, share of points in tasks above 8, estimate-calibration
drift, and people allocated across multiple projects.

Expose GET /api/t/{tenant}/advisor/sprints/{sprintId}/risk returning the assessment for every project
in that sprint.

CONSTRAINT: Follow .claude/rules/backend.md. Every weight and threshold is a named, documented
constant in one place — and any that should be per-tenant reads from tenant settings, not a hardcoded
value. The overload threshold in particular is already a per-tenant setting and must not be
reintroduced as a constant.

RESTRICTION: Do NOT call a model. Do NOT invent a scoring formula and present it as validated — state
plainly that the weights are a starting hypothesis to be tuned against real outcomes. Do NOT hide the
reasons behind the score; the reason list is the product, the number is a sort key.

USAGE: Use the add-endpoint skill. Use plan mode. Delegate review to code-reviewer.

BEHAVIOR: Plan the signals and the weighting before writing, and tell me which signals you expect to
be predictive and which you're including on intuition. Wait for approval. Implement with unit tests
over hand-built histories that assert each signal independently moves the score in the expected
direction. Report which signals the current read model can genuinely support.
```

## Prompt A3 — Semantic Kernel, provider abstraction, and the local stub

```
SCOPE: Introduce Semantic Kernel into Advisor.Core as the orchestration layer, with a provider
abstraction and THREE registrations selected by configuration:
  - OpenAI (direct)                — default for development with a key
  - Azure OpenAI via AI Foundry    — the deployment target; model and version from configuration
  - DeterministicStub              — a custom IChatCompletionService returning fixed, structured,
                                     plausible output with NO network call

Wire the stub as the default when no credential is configured, so `aspire run` works for anyone
cloning the repo. Add an AdvisorKernel abstraction so the rest of Advisor.Core never touches an SDK
type directly. Add SK's telemetry to the existing OpenTelemetry pipeline, with TenantId on every span.

CONSTRAINT: Follow ADR-0023 and ADR-0027. Credentials come from Aspire-injected configuration,
user-secrets in development, environment variables in production — never a literal. Model deployment
name and version are configuration, not constants. Follow .claude/rules/aspire.md for resource wiring.

RESTRICTION: Do NOT hardcode an API key, endpoint, deployment name, or model version anywhere — the
secret-guard hook will block it and it should. Do NOT let SK types leak out of the AI layer into the
facade, business layer, or any ServiceModel. Do NOT make the stub throw or return "not configured" —
it must return usable structured output, or local development is broken and nobody will run this
repo. Do NOT call a real model in a unit test.

USAGE: Verify Semantic Kernel package names, the IChatCompletionService surface, and Azure AI Foundry
connection configuration against current Microsoft documentation before writing — this stack moves
faster than any other part of the system and your recalled API names are likely stale. Use plan mode.
Delegate review to code-reviewer.

BEHAVIOR: Plan the abstraction boundary and show me the interface before writing — specifically, what
Advisor.Core sees and what stays behind the boundary. Wait for approval. Implement, then prove the
stub path works with no credentials configured by running `aspire run` and hitting the endpoint. Tell
me exactly what a contributor with no API key can and cannot exercise.
```

## Prompt A4 — Tenant-partitioned retrieval ← the dangerous one

```
SCOPE: Add retrieval over each tenant's own sprint history so the Advisor can ground its reasoning in
specific past sprints. Use pgvector in advisordb (no new datastore — same reasoning as ADR-0016's
choice of Postgres + jsonb). Embed goal text, retro notes, and outcome summaries. Provide a
SearchTenantHistory operation returning the most relevant past sprints for a given goal.

THE CRITICAL PART: vector similarity search does NOT pass through an EF Core global query filter. The
isolation this system relies on everywhere else is absent here. Tenant partitioning must be enforced
in the query itself — a mandatory tenant predicate applied before the similarity operator, in a place
a caller cannot omit or forget. Design it so that a retrieval call without a tenant is impossible to
express, not merely discouraged.

CONSTRAINT: Follow .claude/rules/tenancy.md and ADR-0024. Embeddings are tenant-scoped rows like any
other. Embedding generation is asynchronous and inbox-idempotent — re-embedding on redelivery wastes
money and must not happen.

RESTRICTION: Do NOT expose a retrieval API that accepts an optional tenant. Do NOT rely on the caller
passing the right tenant — take it from ITenantContext inside the retrieval layer itself. Do NOT
build a shared index across tenants with post-filtering; a top-k that filters after ranking silently
returns fewer or zero results AND has already ranked against another tenant's data. Do NOT let
retrieved text reach a prompt in this prompt — that is A5.

USAGE: Use the add-tenant-scoped-entity skill for the embedding entity. Use plan mode. Delegate review
to code-reviewer AND tenant-isolation-auditor — and tell the auditor explicitly that query filters do
not apply on this path, so it does not report a false clean.

BEHAVIOR: Plan the partitioning strategy and state, in writing, exactly where the tenant predicate is
applied and why it cannot be bypassed. Wait for approval. Implement with tests that: two tenants with
deliberately similar goal text never retrieve each other's rows; a retrieval attempted with no tenant
scope fails loudly rather than returning everything; and re-delivery does not re-embed. Report the
result of the similar-text test specifically — that is the one that matters.
```

## Prompt A5 — The risk brief, end to end

```
SCOPE: Produce the AI-authored sprint risk brief. On a SprintOpened event (or an explicit refresh
request), for each project goal in the sprint: assemble context from the read model and tenant-scoped
retrieval, call the model through the AdvisorKernel, and persist a SprintBrief containing a narrative
assessment, the contributing factors, and a suggested action — alongside the deterministic score from
A2, which is retained and shown, not replaced.

Expose GET /api/t/{tenant}/advisor/sprints/{sprintId}/brief and
POST /api/t/{tenant}/advisor/sprints/{sprintId}/brief/refresh (Member+).

Context assembly is the security boundary of this feature. Build it as a single, testable
ContextAssembler that takes ITenantContext and returns a prompt payload — with every input traceable
to a tenant-scoped source.

CONSTRAINT: Follow ADR-0022 and ADR-0025. Generation is asynchronous, triggered by an event, and
inbox-idempotent including cost — a redelivered SprintOpened must not re-spend tokens. Persist the
model identifier, prompt version, and token counts with every brief. Structured output only: define
the response schema and validate it; a brief that fails validation is discarded and retried, not
displayed. Emit BriefGenerated through the outbox so Audit and Billing see it.

RESTRICTION: **The Advisor is advisory only. It must NOT mutate a goal, task, capacity, plan or any
other service's data, under any circumstance, and must not publish an event that causes another
service to do so.** Do NOT put raw un-templated user text into a prompt without treating it as
untrusted input — goal text is authored by users and a goal reading "ignore previous instructions" is
a realistic thing to receive. Do NOT include any data in the prompt that did not come through the
tenant-scoped assembler. Do NOT display a brief that failed schema validation. Do NOT let a model
failure fail the sprint — the deterministic score must still render.

USAGE: Use the add-endpoint skill. Use plan mode. Delegate review to code-reviewer,
test-gap-analyzer and tenant-isolation-auditor.

BEHAVIOR: Plan the context assembly FIRST and show me every field that will enter the prompt and
where each comes from — I want to read that list before any prompt text is written. Wait for
approval. Then implement, and test: a brief for tenant A contains no tenant B data under deliberately
similar histories; a redelivered event produces no second model call; a malformed model response is
discarded; a model outage degrades to the deterministic score rather than an error. Report token cost
per brief on realistic data.
```

## Prompt A6 — AI credits as a plan limit

```
SCOPE: Meter and limit AI usage per tenant, reusing the existing replicated-quota pattern rather than
inventing a mechanism. Advisor publishes AiUsageRecorded (tokens in, tokens out, model, operation) on
every completed generation. Billing consumes it, maintains a per-tenant monthly credit counter, adds
MaxAiCreditsPerMonth to the plan model, and publishes TenantQuotaChanged as it already does. Advisor
enforces against its LOCAL quota snapshot and refuses with 402 { limit: "MaxAiCreditsPerMonth" }.

Add a usage view to the plan/usage page showing credits consumed against the allowance.

CONSTRAINT: Follow ADR-0017 and ADR-0026. No synchronous call to Billing on the generation path.
Counters are inbox-idempotent — a redelivered usage event must not double-count. Credits reset on the
billing period boundary.

RESTRICTION: Do NOT add a synchronous check. Do NOT let a tenant at zero credits lose the
deterministic risk score — that costs nothing and must keep working; only the AI narrative is gated.
Do NOT meter in tokens in the UI; convert to a unit a customer can reason about and state the
conversion. Do NOT let the overshoot window here go unstated — with a variable-cost resource it is
larger and more expensive than it is for project counts, and that needs saying out loud.

USAGE: Use the add-endpoint skill. Use plan mode. Delegate review to code-reviewer and
tenant-isolation-auditor.

BEHAVIOR: Plan the metering contract and — explicitly — size the overshoot window in credits, not in
milliseconds. Wait for approval. Implement and test: a tenant at its limit gets 402 and still sees
the deterministic score; a double-delivered usage event does not double-count; a quota change
propagates. Report the worst-case overshoot in real money.
```

## Prompt A7 — Evaluation, prompt versioning, and safety via Foundry

*The prompt most likely to be skipped, and the one that separates a demo from a product.*

```
SCOPE: Make the Advisor's quality measurable and its prompts governable, using Azure AI Foundry.
  1. Prompt assets versioned in Foundry, referenced by version from configuration — never inline
     strings in code. The version is persisted with every brief (A5) so any output is reproducible.
  2. An evaluation dataset built from real closed sprints in the demo tenants: goal, context, and the
     KNOWN outcome (slipped / delivered).
  3. An evaluation run scoring the AI brief against the A2 deterministic baseline on the same
     dataset, reporting whether the AI is actually better at predicting slippage — and by how much.
  4. Content safety filters on input and output.
  5. A regression gate: an evaluation run that drops below the recorded baseline fails.

CONSTRAINT: Follow ADR-0023. Evaluation runs against a fixed dataset with a pinned model version, or
the result means nothing. Foundry configuration is injected, never hardcoded.

RESTRICTION: Do NOT ship a prompt change without an evaluation run. Do NOT evaluate on data the
prompt was tuned against. Do NOT report a quality improvement without stating the sample size and the
baseline it beat — "the briefs look good" is not a result. Do NOT let the evaluation dataset contain
one tenant's data used to score another's.

USAGE: Verify Azure AI Foundry's evaluation and prompt-asset APIs against current Microsoft
documentation — this surface is new and moves. Use plan mode. Delegate review to code-reviewer.

BEHAVIOR: Plan the dataset construction and the metric before building — specifically, what counts as
a correct prediction and what the baseline scores. Wait for approval. Implement, run the evaluation,
and report the actual numbers including the AI-versus-deterministic comparison. **If the AI does not
beat the rules-based baseline, say so plainly.** That is a real and useful finding, and shipping the
feature anyway would be the wrong call.
```

## Prompt A8 — The Advisor UI

```
SCOPE: Surface the Advisor in the SPA. On the sprint board, a per-project risk band with the
deterministic reasons always visible. A brief panel showing the AI narrative, its contributing
factors, and the suggested action. A refresh control (Member+). A credits indicator when a tenant is
near its limit.

CONSTRAINT: Follow .claude/rules/frontend.md — zoneless, signals, typed services, one interceptor
owning the slug. The brief loads asynchronously and independently of the board; the board never waits
on it.

RESTRICTION: Do NOT present the AI narrative as fact. Label it as an assessment, show the model and
when it was generated, and make the deterministic reasons visible alongside it — a user must be able
to see WHY without trusting the prose. Do NOT offer a control that applies a suggestion
automatically; the Advisor is advisory (ADR-0022) and a one-click "apply" would undo that decision
through the UI. Do NOT block the board render on a missing, stale, or failed brief. Do NOT hardcode
risk thresholds — they come from the API.

USAGE: Use the add-component skill. Use plan mode. Delegate review to code-reviewer and
api-contract-checker.

BEHAVIOR: Plan the component tree and, specifically, how the UI communicates uncertainty — that's the
design problem here, not the layout. Wait for approval. Implement, run ng test and ng build, and
report how the UI behaves when the brief is absent, stale, or generated by a since-superseded prompt
version.
```

---

# Part 2 — Operational templates

## Template AI-A — Add an Advisor capability

```
SCOPE: Add <capability> to the Advisor — <what it assesses and what it returns>.

CONSTRAINT: Follow ADR-0022 through ADR-0027. It reads only advisordb, is advisory-only, and is
asynchronous and cost-idempotent. Context assembly goes through the existing tenant-scoped
ContextAssembler. Structured output with a validated schema. Prompt versioned in Foundry.

RESTRICTION: Do NOT mutate anything outside advisordb. Do NOT bypass the ContextAssembler. Do NOT add
a new prompt without adding it to the evaluation dataset. Do NOT ship without a deterministic
fallback for when the model is unavailable or the tenant is out of credits.

USAGE: Use the add-endpoint skill.

BEHAVIOR: Show me every field entering the prompt and its tenant-scoped source before writing prompt
text. Wait for approval. Implement, evaluate against the baseline, and report the numbers and the
per-call token cost.
```

## Template AI-B — Audit an AI surface for tenant leakage

*Run this on any change touching context assembly, retrieval, or caching. Your standard isolation
audit does not cover this path.*

```
SCOPE: Audit <the change / the Advisor> for cross-tenant leakage on the AI path specifically.

CONSTRAINT: The five standard layers DO NOT fully apply here. Check these instead, in addition:
  1. Context assembly — is every field traceable to a tenant-scoped source? Any constant, cache,
     global lookup, or "example" embedded in a prompt template that came from real data?
  2. Retrieval — is the tenant predicate applied BEFORE the similarity operator, not after? Is a
     tenant-less call impossible to express?
  3. Embeddings — are stored vectors tenant-scoped rows, and is the index partitioned?
  4. Caching — does any prompt, response, or embedding cache key include TenantId? A shared prompt
     cache is a cross-tenant leak with a fast path.
  5. Evaluation data — does any dataset mix tenants?
  6. Logs and telemetry — do prompt or completion contents reach logs, and if so, whose?
  7. Prompt injection — can user-authored goal text alter instructions or induce disclosure of
     context from the assembler?

RESTRICTION: This is READ-ONLY — report, do not fix. Do NOT report the standard five layers as clean
and stop; on this path they are largely irrelevant. Do NOT downgrade a finding because the leaked
content would be "just a sentence" — a sentence of another tenant's sprint history is a disclosure.

USAGE: Delegate to tenant-isolation-auditor, telling it explicitly that EF query filters do not apply
to vector search or to prompt assembly.

BEHAVIOR: Report findings ranked by severity, each with the specific path by which one tenant's data
could reach another's output. Then list, item by item, which of the seven checks above you performed
and what you found. Then wait.
```

---

## Pro tips

- **The deterministic baseline is the product's floor.** If the model is unavailable, the tenant is out of credits, or a response fails validation, the user still gets a useful risk score. Build it first, keep it visible, never let the AI path be load-bearing for the core promise.
- **Context assembly is the security boundary.** Everywhere else in this system a leak is a row. Here it is a paragraph, and no filter, interceptor, or type system will catch it. Review the assembler's field list the way you'd review a migration.
- **"Idempotent" now has a cost dimension.** A redelivered event that re-runs a handler is correct-but-expensive. Check the token spend on every retry path.
- **Evaluate before you believe it.** A7 exists because "the briefs read well" is the easiest way to ship a feature that is worse than arithmetic. If the baseline wins, that's the finding — publish it.
- **Verify the SDK surface, always.** Semantic Kernel and Foundry move faster than anything else in this stack. Recalled API names in this area are stale more often than not.
