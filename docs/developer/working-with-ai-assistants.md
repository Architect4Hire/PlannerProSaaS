# Working with AI Assistants

*PlannerPro supports **Claude Code and GitHub Copilot side by side**. This is not a migration — both are first-class, and a contributor using either gets the same rules, the same procedures, and the same guardrails.*

---

## The design in one sentence

**One canonical instruction file, one source of truth for rules and agents, one shared skills folder, and thin per-tool shims** — because two hand-maintained toolkits drift within a week, and the drift is silent.

## Who reads what

| File / folder | Claude Code | Copilot | Notes |
| --- | :-: | :-: | --- |
| `AGENTS.md` | via `@import` | ✅ native | **The canonical constitution.** All shared rules live here |
| `CLAUDE.md` | ✅ native | ✅ (coding agent) | Thin shim: `@AGENTS.md` + Claude-only notes |
| `.github/copilot-instructions.md` | ❌ | ✅ | Thin overlay: Copilot-only notes |
| `.claude/skills/**/SKILL.md` | ✅ native | ✅ **native** | **Shared. No duplication.** |
| `.claude/rules/*.md` | ✅ native | ❌ | Source of truth for path-scoped rules |
| `.github/instructions/*.instructions.md` | ❌ | ✅ | **Generated** from `.claude/rules/` |
| `.claude/agents/*.md` | ✅ native | ❌ | Source of truth for review agents |
| `.github/agents/*.agent.md` | ❌ | ✅ | **Generated**. Must end `.agent.md` or it is silently ignored |
| `.claude/hooks/*.sh` | ✅ | ✅ | **Shared scripts**, wired by two config files |
| `.claude/settings.json` | ✅ | ✅ (VS Code) | Claude hook wiring |
| `.github/hooks/plannerpro.json` | ❌ | ✅ | Copilot hook wiring; must be on the **default branch** |
| `.github/workflows/guardrails.yml` | — | — | **Tool-agnostic.** The only *enforcing* layer |

Verify this table when either tool ships a release — vendor support shifts, and it has shifted twice already.

## Adding something new — where does it go?

| You want to add | Put it in | Then |
| --- | --- | --- |
| A **procedure** ("how we add an endpoint") | `.claude/skills/<name>/SKILL.md` | Nothing. Both tools read it |
| A **rule** for a path | `.claude/rules/<area>.md` | `python3 .github/sync-copilot.py` |
| A **review agent** | `.claude/agents/<name>.md` | `python3 .github/sync-copilot.py` |
| A **shared constraint** | `AGENTS.md` | Nothing |
| A **hard guarantee** | `.github/workflows/guardrails.yml` | Nothing — CI is the only real enforcement |
| A **tool-specific note** | `CLAUDE.md` or `.github/copilot-instructions.md` | Nothing |

**Prefer skills.** They're the one artifact both tools read from one location, so anything expressible as a skill costs half as much to maintain as anything expressible as a rule.

## The generator

`.github/sync-copilot.py` regenerates `.github/instructions/` and `.github/agents/` from `.claude/`. Generated files carry a DO-NOT-EDIT banner. `--check` fails CI when they're stale.

It also **converts two things that don't translate**:

- **Negated globs.** `.claude/rules` supports `!src/Foo/**`; Copilot's `applyTo` does not. The generator drops negations and **warns** — if you see that warning, rewrite the source as positive globs or the rule will silently over-apply. This already happened once, in `backend.md`.
- **Tool declarations.** Claude's `tools: Read, Grep, Glob, Bash` becomes Copilot's tool array, read-only in both.

## Two known frictions

**Hooks fire twice in VS Code Copilot.** It loads hooks from `.claude/settings.json` *and* `.github/hooks/*.json`, and runs every entry from every source. All three scripts are idempotent, so this was cosmetic rather than dangerous — but duplicate warnings train people to ignore warnings, so `format.sh` and `tenancy-guard.sh` carry a path+mtime dedupe guard with a 5-second window. A genuine re-edit still re-runs.

**Review agents behave differently.** The same agent definition produces different depth in each tool. Treat agent output as a prompt to look, not as a verdict — in both.

## What is actually enforced

Only CI. Rules and instructions are *guidance* a model can ignore; hooks only fire inside an agent session and do nothing for a human commit or a cloud-agent PR that bypasses them.

`guardrails.yml` re-implements the tenancy guards as blocking checks: no `FindAsync` on tenant-scoped entities, no stray `IgnoreQueryFilters`, no unique index missing `TenantId`, plus the generator sync check and build/test. **If a rule matters enough that a leak would be unacceptable, it belongs there — not only in a rules file.**

## Onboarding

```bash
chmod +x .claude/hooks/*.sh
python3 .github/sync-copilot.py --check      # confirm the toolkits are in sync
```

**Claude Code:** `/memory` shows the loaded rules, `/agents` lists the subagents.
**Copilot:** `/skills` lists loaded skills; a chat response's References list should show `copilot-instructions.md` or `AGENTS.md`.

Then start from [`docs/prompts/scrub-prompts.md`](../prompts/scrub-prompts.md) — the prompts are written in SCRUB form and work in either tool, because they describe intent and constraints rather than tool mechanics.
