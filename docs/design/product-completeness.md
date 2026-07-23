# PlannerPro — Product Completeness: Feature Gaps to a Sellable SaaS

*The complementary document to [Build Plan & Risks](./build-plan-and-risks.md). That one asks "is the architecture sound and sequenced correctly?" This one asks "would a customer pay for it?" — capability by capability, each named to the service that owns it, with a value-tiered build order.*

---

## 0. How this differs from the build plan

| | Build Plan & Risks | This document |
| --- | --- | --- |
| Question | Is it correct, safe, and operable? | Is it *worth buying*? |
| Unit | Mechanism, risk, sequence | Capability, user journey |
| Owner | Architect | Product |
| Failure it prevents | A tenant leak, a silent stale read model | A demo that impresses engineers and closes nothing |

Both matter. A perfectly isolated system nobody wants is still a hobby.

---

## 1. The completeness verdict

The **planning core** — sprints, goals, tasks, effort, overload, roadmap, capacity — is a real product that solved a real problem in its single-tenant life. That is a genuine head start: most SaaS attempts start with billing and never reach a useful core.

What's missing is everything between "a good internal tool" and "a thing a stranger can sign up for, understand, and pay for." The SCRUB prompt library builds much of that (signup, invitations, branding, limits), but several capabilities a buyer would assume exist are not in scope anywhere yet.

### Completeness scorecard (product, not engineering)

| Capability | State | Owner |
| --- | :-: | --- |
| Sprint board, goals, tasks, effort, overload | 🟢 Designed, proven concept | Planning |
| Program roadmap | 🟢 Designed | Roadmap |
| Capacity planning | 🟢 Designed | Planning |
| Client → project hierarchy | 🟢 Designed | Portfolio |
| Self-serve signup | 🟡 In prompts (13) | Access |
| Invitations & team management | 🟡 In prompts (13) | Access |
| White-label branding | 🟡 In prompts (14) | Access + Files |
| Plans, limits, trial lifecycle | 🟡 In prompts (10) | Billing |
| Platform admin console | 🟡 In prompts (16) | — |
| **Taking money** | 🔴 Modelled, not wired | Billing |
| **Email delivery** | 🔴 Stubbed | Notifications |
| **Onboarding / empty states** | 🔴 Not scoped | Web |
| **Reporting & export** | 🔴 Barely scoped | Planning |
| **Search / filtering at scale** | 🔴 Not scoped | Planning, Portfolio |
| **Notification preferences** | 🔴 Not scoped | Notifications |
| **Mobile / responsive** | 🔴 Not scoped | Web |
| **Integrations** | ⚪ Explicitly out of scope | — |

---

## 2. The three that block revenue

**2.1 Nobody can pay 🔴 — the headline gap.** ADR-0018 models plans, limits and lifecycle but wires no provider. Every enforcement point exists; the checkout does not. Until it does, there is no revenue and the trial lifecycle terminates in a manual conversation. *The work is a webhook that writes `Tenant.Status` and three reserved columns — deliberately small by design, but it is not zero and it is not started.*

**2.2 No email 🔴.** Invitations are the primary growth loop: an owner signs up, invites four colleagues, and the product spreads. With `IInvitationNotifier` logging to the console, that loop is broken outside development. Trial-expiry and payment-failure notices have the same problem. *Owner: Notifications. Blocking for Phase 3 onward.*

**2.3 Nothing happens after signup 🔴.** A new tenant lands on an empty board with three sample rows and no idea what to do. Onboarding — a first-run checklist, meaningful empty states, a sample sprint that demonstrates the overload warning — is the difference between a trial that converts and one that bounces on day one. *Not scoped anywhere. Owner: Web + Access.*

---

## 3. Close the product loop

**3.1 Reporting and export.** Owners of agencies need to show clients what was delivered. A sprint summary, a per-client effort report, and CSV/PDF export are table stakes for the agency model the product is sold on. Currently: tenant JSON export exists in Prompt 16 as an *operational* feature, not a product one. Those are different things.

**3.2 Search and filtering at scale.** The board and timeline assume a handful of projects. A tenant with forty projects across twelve clients needs filtering, saved views, and search. The client filter in Prompt 12 is a start, not an answer.

**3.3 Notification preferences.** Once email works, users will want to control it. A notification service with no preference model generates unsubscribes, not engagement.

**3.4 Task detail depth.** Labels, descriptions, assignees, and comments are absent. Whether they belong here is a genuine product question — PlannerPro is deliberately *not* a ticket tracker, and adding them risks becoming a worse Jira. Worth deciding explicitly rather than drifting into.

---

## 4. Account and identity lifecycle

Currently thin, and it shows up fast in real use:

- **Password reset** — not scoped. A single missing flow that generates support tickets from day one.
- **Email verification** at signup — not scoped. Without it, typo'd addresses create orphan tenants.
- **Leaving a tenant** — memberships can be removed by an admin, but a user cannot leave.
- **Transferring ownership** — in the role matrix, needs a flow.
- **Deleting an account** across tenants — interacts with the append-only audit trail (ADR-0016) in a way nobody has designed.

---

## 5. Features that require an architectural decision

Propose, don't scaffold. Each of these would change the shape of the system:

| Feature | Why it needs an ADR |
| --- | --- |
| **SSO / SAML** | Changes ADR-0010's global-identity model; memberships would map to external identities |
| **Custom domains** | Reopens ADR-0007; per-tenant TLS is a large operational surface |
| **Public API + tenant API keys** | A second authentication scheme; ADR-0021 has no story for non-browser clients |
| **Real-time collaboration** | Websockets through a gateway designed for request/response |
| **Per-tenant data residency** | Would likely force ADR-0008 toward database-per-tenant |
| **Usage-based pricing** | ADR-0018 assumes a seat/limit-shaped world |

---

## 6. Suggested build order (product value ÷ effort)

**Tier 1 — makes it sellable.** Email delivery · payment checkout · onboarding and empty states · password reset.
*Rationale: without these there is no growth loop, no revenue, and no first-week retention. Everything else is polish on a product nobody completed signup for.*

**Tier 2 — makes it retainable.** Reporting and export for client-facing work · search and filtering · notification preferences · email verification.

**Tier 3 — makes it competitive.** Task detail depth (if decided) · saved views · mobile-responsive layouts · richer capacity analytics.

**Tier 4 — makes it enterprise.** SSO · custom domains · public API · audit export for compliance.

---

## 7. Relationship to the rest of the docs

- The [Build Plan & Risks](./build-plan-and-risks.md) sequences the *engineering*; this sequences the *product*. Tier 1 here does not start until the Prompt 8 isolation gate is green — shipping a growth loop on top of an unverified isolation model would be a spectacularly bad trade.
- Items in §5 each need an entry in [`docs/adr/`](../adr/README.md) before implementation.
- `multitenancy-plan.md` §12 lists what was explicitly ruled out; that list and §5 here should stay in agreement.

### Bottom line

The planning core is good and the SaaS scaffolding is designed. What stands between this and a business is unglamorous: email, payment, onboarding, and password reset. None of it is architecturally interesting, all of it is required, and it is the work most likely to be deferred in favour of another well-drawn diagram.
