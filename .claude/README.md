# The `.claude/` folder

This folder is the reusable Claude Code toolkit for **PlannerPro** — a multi-tenant, white-label SaaS
built as **Aspire + ASP.NET Core microservices + Angular**, orchestrated locally by Aspire. The
`.claude/` toolkit is as much the point of the repo as the app itself.

> **Important:** the project's main memory file, `CLAUDE.md`, lives at the **repo root**, one level
> *above* this folder — not inside it. Claude Code auto-discovers `CLAUDE.md` by walking up from your
> working directory, and the root copy survives `/compact`.

## What each piece is, and when it loads

| Path | What it is | When it enters context |
|---|---|---|
| `settings.json` | Shared project settings (incl. hook wiring). Committed. | Read at session start. |
| `rules/tenancy.md` | **The spine.** Isolation model, the two legal sources of `TenantId`, the five layers, the traps. Path-scoped to Shared, Contracts, Gateway, every `.Core/Data`, every Domain folder, and every consumer. | Loads on anything that could leak. |
| `rules/aspire.md` | AppHost/orchestration conventions (per-service DBs, emulators, gateway). Path-scoped to AppHost/ServiceDefaults. | When Claude touches the AppHost. |
| `rules/backend.md` | Per-service layered conventions + the host/`.Core` split. Path-scoped to the services. | When Claude touches a service. |
| `rules/messaging.md` | Outbox/inbox, dispatcher, envelope-derived consumer scope, event contracts. Path-scoped to Shared, Contracts, consumers. | When Claude touches messaging. |
| `rules/gateway.md` | YARP conventions + tenancy resolution and header projection. Path-scoped to the gateway. | When Claude touches the gateway. |
| `rules/billing.md` | Plans, limits, and the quota-replication contract. Path-scoped to Billing and the facades that enforce. | When Claude touches limits. |
| `rules/audit.md` | Correlation/causation/actor and the append-only trail. Path-scoped to Contracts, Gateway, Audit, consumers, business. | When Claude touches the trail. |
| `rules/frontend.md` | Angular conventions (zoneless, signal-first, one interceptor owns the slug). Path-scoped to `src/web/`. | When Claude touches the web app. |
| `skills/add-endpoint/` | Playbook for an API endpoint or event consumer in a service. | On demand, when the task matches. |
| `skills/add-tenant-scoped-entity/` | Playbook for a new entity: `ITenantScoped`, tenant-scoped indexes, migration. | On demand. |
| `skills/add-component/` | Playbook for an Angular component. | On demand. |
| `skills/add-aspire-resource/` | Playbook for declaring a locally-orchestrated resource. | On demand. |
| `skills/add-audit-event/` | Playbook for closing an audit coverage gap. | On demand. |
| `skills/trace-a-request/` | Support workflow: reconstruct a request cradle-to-grave from the trail. | On demand. |
| `agents/tenant-isolation-auditor.md` | **Read-only auditor for cross-tenant leakage.** Run on any new entity, endpoint, consumer, query, migration or route. | When delegated, or `@tenant-isolation-auditor`. |
| `agents/code-reviewer.md` | Read-only reviewer (microservice- and boundary-aware). | When delegated, or `@code-reviewer`. |
| `agents/test-gap-analyzer.md` | Read-only test-gap subagent (flags missing isolation/idempotency/rollback tests). | When delegated, or `@test-gap-analyzer`. |
| `agents/api-contract-checker.md` | Read-only subagent: ServiceModel↔Angular drift *and* event↔consumer drift. | When delegated, or `@api-contract-checker`. |
| `hooks/format.sh` | Formats the edited file after each edit. | `PostToolUse`. |
| `hooks/secret-guard.sh` | **Blocks** edits containing anything credential-shaped. | `PreToolUse`. |
| `hooks/tenancy-guard.sh` | **Warns** (never blocks) on known leak vectors: `FindAsync`, `IgnoreQueryFilters`, an unscoped unique index, a global `Users` query. | `PostToolUse`. |

Rule of thumb:
- **Rule / CLAUDE.md** = something Claude should *know*.
- **Skill** = a procedure Claude should *follow* when a task matches.
- **Subagent** = work Claude should *delegate* to keep the main context clean.
- **Hook** = something that must happen *no matter what Claude decides*.

`tenancy-guard.sh` warns rather than blocks on purpose. Every pattern it catches has a legitimate use
in a bypass context, so it raises the flag and leaves the judgement to you — a blocking hook here
would train people to work around it, which is worse than a warning they read.

## Not committed (personal / local)
- `settings.local.json` — personal overrides, git-ignored on purpose.
- Anything ending in `.local.*`.

## After cloning
```bash
chmod +x .claude/hooks/*.sh
```
Then open a session: `/memory` confirms the rules load, `/agents` shows the subagents. The scaffolding
sequence is in `docs/prompts/scrub-prompts.md` — run Part 1 in order.

## The mismatch to expect on a fresh repo
This toolkit ships *before* `src/`. The skills and hooks call `dotnet`/`ng`/`aspire`; they're harmless
no-ops until those tools and the projects exist. `docs/prompts/scrub-prompts.md` is how you create
`src/`.

## Verify before trusting
Aspire, the Azure Service Bus emulator, Azurite, YARP, and Claude Code all ship fast. The
`settings.json` hook syntax, subagent frontmatter, and exact API names (`AddAzureServiceBus`,
`RunAsEmulator`, `AddAzureStorage`, `AddAzureServiceBusClient`, `AddJavaScriptApp`, YARP wiring) are
the most likely things to drift — confirm against https://code.claude.com/docs and https://aspire.dev.

**One thing to verify with unusual care:** EF Core's global query filter closing over an injected
scoped `ITenantContext`, and how that interacts with the compiled-model cache and any context pooling.
That interaction is the load-bearing assumption of the whole isolation model, and it is exactly the
kind of thing that changes between EF versions. Prove it with a test, don't assume it.
