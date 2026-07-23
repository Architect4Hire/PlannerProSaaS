# PlannerPro — Build Plan & Risk Register

*The analogue of JobBoard's "Ongoing Architecture Plan," adjusted for the fact that **`src/` does not exist yet**. JobBoard's review scores a running spike; this one scores a plan and sequences the work that turns it into one. Re-score this document once Prompt 15 (end-to-end verification) has run.*

---

## 0. Verdict up front

The design is sound and the decisions are documented, but **nothing is built and two assumptions are unproven**. The plan's quality is therefore the plan's only current asset — and two items in it could invalidate a week of work if left unverified.

Two things must happen before the fan-out (Prompt 9): the EF query-filter mechanism must be **proven with a test**, and the bus-scoping mechanism must be **proven with a test**. Everything else in this system is a variation on patterns that already work in JobBoard. Those two are new, and both fail silently.

### Scorecard

| Dimension | Score | Note |
| --- | :-: | --- |
| Boundary definition | 🟢 | Eight contexts, each with one reason to change; documented in ADR-0001 |
| Tenancy design | 🟢 | Five layers, two legal sources of `TenantId`, defense in depth |
| Tenancy **verification** | 🔴 | The two load-bearing mechanisms are assumptions until tested |
| Messaging design | 🟢 | Outbox/inbox pattern proven in JobBoard; carried over intact |
| Contract discipline | 🟢 | Envelope enforced by the compiler (ADR-0013) |
| Edge / gateway | 🟠 | Design is right; **network-level enforcement is unaddressed** |
| Observability | 🟠 | `TenantId` on spans is designed, not built |
| Testing posture | 🟠 | Cross-tenant suite is specified and gated (Prompt 8) but unwritten |
| Deployment | 🔴 | No story at all |
| Operability | 🔴 | No retention, no reconciliation, no runbook |

---

## 1. The two unproven assumptions

Everything else here is engineering. These two are research.

### 1.1 EF Core global filters over an injected scoped context 🔴

ADR-0008 rests on `HasQueryFilter` closing over an **injected** `ITenantContext` and re-evaluating per request. That interacts with EF's compiled-model cache and with any context pooling, and the behaviour is version-sensitive.

**Failure mode:** the filter captures the first tenant the model ever sees and applies it to every subsequent request. Every tenant sees tenant one's data. Nothing throws.

**Mitigation:** Prompt 2 requires writing out the expected evaluation semantics *before* coding and proving them with a test that exercises two tenants through the same pooled context. Do not let this be skipped, and re-run it on every EF upgrade.

### 1.2 Consumer scope from the event envelope 🔴

ADR-0009 is the decision with no counterpart in the single-tenant design. A consumer has no HTTP request, so the processor host must establish `ITenantContext` from the envelope before invoking it.

**Failure mode A:** context is unset, the filter matches nothing, every consumer silently does nothing. Read models go stale and quotas stop counting, with no error anywhere.
**Failure mode B:** `BypassFilters` defaults on, and consumers read and write across every tenant.

**Mitigation:** Prompt 3 requires this to be central and unbypassable, with tests for both failure modes plus dead-lettering of an envelope-less message. The `tenant-isolation-auditor` checks the bus path specifically.

---

## 2. Sequencing — and why this order

The prompt library's order is the plan. Its shape is deliberate:

| Phase | Prompts | Why here |
| --- | --- | --- |
| Spine | 0–1 | Nothing is testable until persistence and the outbox exist |
| **Tenancy mechanism** | **2–3** | Both unproven assumptions, isolated, before anything depends on them |
| Edge | 4 | Resolution and header projection, the other half of tenancy |
| Template service | 5 | Access first — it's the hardest (mixed scoping), so the pattern is proven at its worst |
| Prove the loop | 6 | Provisioning exercises outbox, dispatcher, envelope scope, inbox, filters and interceptor at once |
| Core product | 7 | Planning — the thing the product is for |
| **Isolation gate** | **8** | The fan-out is blocked behind a green cross-tenant suite |
| Fan-out | 9–10 | One service at a time, suite re-run after each |
| Client | 11–14 | Shell, screens, onboarding, white-label |
| Verify | 15 | Honest end-to-end inventory before any repair |
| Operate | 16 | Platform surface, rate limits, export |

**The gate at Prompt 8 is the most important structural choice in the plan.** A leak found after the fan-out has been copied into six more services; found before, it is one fix in `Shared`.

**Access before Planning** is deliberate and slightly counterintuitive: it is the service with mixed global/tenant-scoped tables, so proving the pattern there proves it everywhere. Building the easy service first would teach less.

---

## 3. Risk register

| # | Risk | Severity | Mitigation | Status |
| --- | --- | :-: | --- | --- |
| 1 | Services reachable off-gateway ⇒ header spoofing ⇒ **total isolation bypass** | 🔴 Critical | Network policy / mTLS enforcing gateway-only ingress; or supersede with a signed internal token (ADR-0011, ADR-0021) | **Unaddressed** |
| 2 | EF filter captures a stale tenant (§1.1) | 🔴 Critical | Prove with a two-tenant pooled-context test in Prompt 2 | Planned |
| 3 | Consumer scope not established (§1.2) | 🔴 Critical | Central, unbypassable scope in the processor host; tests for both failure modes | Planned |
| 4 | A new entity misses `ITenantScoped` | 🟠 High | Reflection applies filters automatically; reflection test enumerates and asserts; `tenancy-guard.sh`; auditor subagent | Planned |
| 5 | `FindAsync` on scoped data bypasses filters | 🟠 High | Rule, hook warning, subagent check, and a build-failing test (Prompt 8) | Planned |
| 6 | Globally unique index leaks existence via constraint violation | 🟠 High | `(TenantId, …)` everywhere; hook warns on unique indexes lacking `TenantId` | Planned |
| 7 | Read model silently stale after a dead-lettered event | 🟠 High | **No reconciliation mechanism exists.** Needs design (ADR-0014) | **Open gap** |
| 8 | Membership cache TTL delays revocation | 🟠 High | Short TTL, invalidate on membership change, state the window | Planned |
| 9 | Outbox / inbox / `auditdb` grow unbounded | 🟠 High | Retention and rollup jobs; none designed yet | **Open gap** |
| 10 | No deployment story | 🟠 High | Out of current scope; blocks anything real | **Open gap** |
| 11 | Quota overshoot window unsized | 🟡 Medium | Measure it, document it, decide commercially (ADR-0017) | Planned |
| 12 | Cache key omits `TenantId` | 🟡 Medium | Cross-tenant leak with a fast path; explicit in HLD §8.4 and reviewed | Planned |
| 13 | Emulator behaviour differs from real Service Bus | 🟡 Medium | Verify dead-lettering and retry against the real broker before trusting them | **Open gap** |
| 14 | Anonymous branding endpoint confirms slug existence | 🟢 Low | Accepted trade; generic defaults for unknown slugs, rate-limited (ADR-0019) | Accepted |
| 15 | Agent drift across eight services | 🟡 Medium | Constitution, path-scoped rules, skills, four read-only subagents, ordered prompts | Mitigated by design |

---

## 4. What "done" means for each phase

Prompts are not complete because code compiles. Each phase has an acceptance condition:

- **Prompts 2–3:** a test proves the mechanism under the specific failure mode it exists to prevent — not that it works on the happy path.
- **Prompt 6:** two tenants provisioned for real via `aspire run`; each has its own client; a redelivery is a no-op; tenant A's client is invisible from B.
- **Prompt 8:** the cross-tenant suite is green, and the reflection test actually enumerates types rather than passing vacuously.
- **Prompts 9–10:** the suite is re-run and green **after each service**, not once at the end.
- **Prompt 15:** an honest inventory — what was exercised, what passed, what failed, what could not be verified and why. Failures ranked with isolation first regardless of apparent size.

---

## 5. What *not* to do (guarding the build)

- **Don't fan out before Prompt 8 is green.** The gate is the plan's main defense.
- **Don't fix an isolation finding at the endpoint.** A leak at one endpoint means the mechanism failed; fix it in `Shared` and re-run everywhere.
- **Don't add a synchronous cross-service call** to make a limit hard or a read fresh. That is ADR-0002 and ADR-0017 being undone one convenience at a time.
- **Don't relax 404-to-403** for a better error message. The message is the leak.
- **Don't `IgnoreQueryFilters()` to make a test pass.** If that fixes it, a scoping bug has been found and hidden.
- **Don't add a ninth service** without an ADR. Eight was a decision, not an accident.
- **Don't batch the fan-out.** One service, one review, one suite run.

---

## 6. Next three actions

1. **Prove assumption §1.1** — a two-tenant test through a pooled context, before anything is built on it.
2. **Design the network boundary** — decide now whether risk #1 is closed by network policy or by superseding ADR-0011/0021 with a signed internal token. It affects the gateway's shape, so deciding late is expensive.
3. **Design read-model reconciliation** (risk #7) — a dead-lettered event currently leaves a read model permanently, silently wrong, and there is no repair path.

### Bottom line

The decisions are good and the sequencing is right. The plan's honest weakness is that its two newest mechanisms are unproven and its production boundary is undefined. Close those three items and this is a build; leave them and it is a demo with a documented hole.
