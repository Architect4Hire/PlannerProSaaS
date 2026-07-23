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

**The delivery model changed, and it changes this document.** PlannerPro is a tool Architect4Hire's
clients log into as part of an engagement — not a public SaaS a stranger discovers, signs up for, and
pays with a card. Tenants are provisioned by a platform admin (Prompt 14). There is no self-serve
signup, and there is deliberately no checkout.

That deletes the two gaps this document used to lead with, and promotes one that was third.

The **planning core** — sprints, goals, tasks, effort, overload, roadmap, capacity — is a real product
that solved a real problem in its single-tenant life. The question is no longer "would a stranger buy
this?" It is **"can a client be onboarded and get value in their first week without you sitting next
to them?"** Everything below is scored against that.

### Completeness scorecard (product, not engineering)

| Capability | State | Owner |
| --- | :-: | --- |
| Sprint board, goals, tasks, effort, overload | 🟢 Designed, proven concept | Planning |
| Program roadmap | 🟢 Designed | Roadmap |
| Capacity planning | 🟢 Designed | Planning |
| Client → project hierarchy | 🟢 Designed | Portfolio |
| Client provisioning (admin) | 🟡 In prompts (14) | Access |
| Invitations & team management | 🟡 In prompts (14) | Access |
| White-label branding | 🟡 In prompts (15) | Access + Files |
| Plans, limits, lifecycle | 🟡 In prompts (10) | Billing |
| Public front door & login handoff | 🟡 In prompts (11–12, 16–18) | Web + Gateway |
| Deployment | 🟡 In prompts (20) | — |
| Platform admin console | 🟡 In prompts (22) | — |
| **Onboarding / empty states** | 🔴 Not scoped | Web |
| **Password reset** | 🔴 Not scoped | Access |
| **Reporting & export** | 🔴 Barely scoped | Planning |
| **Search / filtering at scale** | 🔴 Not scoped | Planning, Portfolio |
| **Mobile / responsive** | 🔴 Not scoped | Web |
| **Email delivery** | 🟠 Stubbed — now a convenience, not a blocker | Notifications |
| **Notification preferences** | 🟠 Not scoped — blocked behind email | Notifications |
| **Taking money** | ⚪ Out of scope — invoiced through the engagement | Billing |
| **Self-serve signup** | ⚪ Deliberately out of scope | — |
| **Integrations** | ⚪ Explicitly out of scope | — |

---

## 2. What the client-tool model deleted — and what it promoted

**Deleted: taking money ⚪.** ADR-0018's plan model, limits and lifecycle all still matter — they
constrain what a client can do. But the checkout does not exist and does not need to. You invoice
through the consulting relationship. The three reserved Stripe columns stay null exactly as designed,
and adding checkout later remains a webhook rather than a refactor.

**Deleted: self-serve signup ⚪.** Prompt 14 provisions tenants from the platform-admin surface. No
public `/api/signup`, no slug-availability endpoint, no anonymous trial provisioning. This is a real
scope reduction, not a deferral — and it is the reason Prompt 14 is smaller than the prompt it
replaced.

**Downgraded: email 🟠.** It was the growth loop; now it is a convenience. You onboard a client's
owner in a call. Invitations still want it — an owner adding four colleagues shouldn't require you —
but `IInvitationNotifier` logging a link in development is survivable for the first few clients in a
way it never would be for public signup.

**Promoted: onboarding 🔴 — now the headline gap.** A newly provisioned client's owner logs in and
lands on a board with sample rows and no idea what to do. Under the old model that was a bounced
trial. Under this one it is *you* on a call walking them through it, every time, for every new client
— which is the cost that compounds fastest as you add clients. A first-run checklist, meaningful empty
states, and a sample sprint that visibly trips the overload warning would repay themselves on the
second client.

**Promoted: password reset 🔴.** Nowhere in scope. Under public signup this was table stakes. Under
this model it is worse: every forgotten password is a support request routed to *you personally*,
because there is nobody else to route it to.

---

## 3. Close the product loop

**3.1 Reporting and export.** Owners of agencies need to show clients what was delivered. A sprint summary, a per-client effort report, and CSV/PDF export are table stakes for the agency model the product is sold on. Currently: tenant JSON export exists in Prompt 22 as an *operational* feature, not a product one. Those are different things.

**3.2 Search and filtering at scale.** The board and timeline assume a handful of projects. A tenant with forty projects across twelve clients needs filtering, saved views, and search. The client filter in Prompt 13 is a start, not an answer.

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
| **Self-serve signup + checkout** | Reverses the client-tool delivery model; reinstates the public `/api/signup` surface Prompt 14 deliberately omits |

---

## 6. Suggested build order (product value ÷ effort)

**Tier 1 — makes a client self-sufficient.** Onboarding and empty states · password reset · email
delivery for invitations.
*Rationale: these are the three things that determine whether onboarding client number four costs you
a phone call. Nothing here is architecturally interesting, all of it is required, and it is the work
most likely to be deferred in favour of another well-drawn diagram.*

**Tier 2 — makes the tool worth keeping.** Reporting and export so an agency can show *their* client
what was delivered · search and filtering once a tenant passes ~20 projects · email verification.

**Tier 3 — makes it competitive.** Task detail depth (if decided) · saved views · mobile-responsive
layouts · richer capacity analytics.

**Tier 4 — only if the model changes.** Self-serve signup · payment checkout · SSO · custom domains ·
public API. Each of these is a *business model* decision before it is a feature, and each needs an
ADR — see §5.

---

## 7. Relationship to the rest of the docs

- The [Build Plan & Risks](./build-plan-and-risks.md) sequences the *engineering*; this sequences the *product*. Tier 1 here does not start until the Prompt 8 isolation gate is green — shipping a growth loop on top of an unverified isolation model would be a spectacularly bad trade.
- Items in §5 each need an entry in [`docs/adr/`](../adr/README.md) before implementation.
- `multitenancy-plan.md` §12 lists what was explicitly ruled out; that list and §5 here should stay in agreement.

### Bottom line

The planning core is good and the scaffolding is designed. As a **client tool**, the distance to useful is much shorter than it was as a public SaaS — payment and self-serve signup are gone, and email is a convenience. What remains is unglamorous and unavoidable: **onboarding, password reset, and reporting a client can hand to their own client.** That is three pieces of work, none of them architecturally interesting, all of them the difference between a tool your clients use and a tool you demo.
