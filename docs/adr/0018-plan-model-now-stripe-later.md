# ADR-0018: Model plans and limits now; wire Stripe later

- **Status:** Accepted
- **Date:** 2026-07-23
- **Deciders:** Robert Felkins
- **Related:** ADR-0017 (quota replication), ADR-0020 (suspension), `.claude/rules/billing.md`

## Context

The product needs plans, limits, trials, and a subscription lifecycle to be a SaaS at all. It does not yet need to take money. Building payment processing before the product is validated is effort spent on the least uncertain part of the problem — but building *without* a plan model means retrofitting one into every enforcement point later, which is a refactor rather than an addition.

## Decision

**Build the full plan, limit, and lifecycle model now. Reserve the payment-provider columns and leave them unused.**

- Seeded plans: `free` (3 users / 1 client / 3 projects / 100 MB), `team` (15 / 10 / 30 / 5 GB), `business` (100 / unlimited / unlimited / 50 GB). Prices are a product decision, deliberately not fixed here.
- Full lifecycle: `Trialing → Active → PastDue → Suspended → Cancelled`, enforced in the product (ADR-0020).
- `StripeCustomerId`, `StripeSubscriptionId`, and `CurrentPeriodEndsAt` **exist on `Tenant` and stay `NULL`**.
- **No Checkout, no webhooks, no payment provider wired.**

## Consequences

**Positive**
- Adding Checkout later is a webhook that writes `Tenant.Status` and three columns — additive, not a refactor. Every enforcement point already exists.
- The trial and suspension lifecycle can be exercised end to end without money moving.
- No PCI surface, no provider dependency, no test-mode complexity in a showcase repo.

**Negative**
- **Three columns that do nothing are a smell** to anyone reading the schema cold, and need the comment that explains them. Reserved fields have a habit of never being used, at which point they are just debt.
- Untested integration assumptions: the model presumes a Stripe-shaped world (one customer, one subscription, period end). A different provider, or usage-based pricing, would not fit as neatly as this ADR implies.
- Plan changes are currently an internal operation with no payment path, so the admin surface is doing a job billing should eventually do.

**Neutral**
- Prices being TBD is honest; they are a business decision that shouldn't be invented in a schema.

## Alternatives considered

- **Wire Stripe now.** Rejected: significant effort, a live dependency, and webhook handling in a repo with no revenue. It would also make the showcase harder to run.
- **No plan model until billing is real.** Rejected: enforcement points (ADR-0017) would have to be retrofitted into four services, which is the expensive version of this work.
- **Plan model with no reserved columns.** Marginally cleaner schema; adding three nullable columns later is a trivial migration. A reasonable objection to this ADR — the reserved columns are as much a statement of intent as a technical necessity.
