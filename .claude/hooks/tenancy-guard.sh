#!/usr/bin/env bash
# PostToolUse hook: warn (never block) when an edited C# file contains a pattern that
# is a known cross-tenant leak vector. Advisory by design — there are legitimate uses in
# bypass contexts, so this raises the flag rather than making the call.
set -euo pipefail

input="$(cat)"

if command -v jq >/dev/null 2>&1; then
  file="$(printf '%s' "$input" | jq -r '.tool_input.file_path // empty')"
else
  file="$(printf '%s' "$input" \
    | grep -o '"file_path"[[:space:]]*:[[:space:]]*"[^"]*"' \
    | head -1 | sed 's/.*"file_path"[[:space:]]*:[[:space:]]*"//; s/"$//')"
fi

[ -z "${file:-}" ] && exit 0
[ -f "$file" ] || exit 0
case "$file" in *.cs) ;; *) exit 0 ;; esac

warn() { echo "tenancy-guard: $file — $1" >&2; }

grep -Eq '\.(FindAsync|Find)\(' "$file" && \
  warn "uses Find/FindAsync. These bypass the tenant query filter on tracked entities. Use FirstOrDefaultAsync unless this is provably not a tenant-scoped entity."

grep -q 'IgnoreQueryFilters' "$file" && \
  warn "uses IgnoreQueryFilters. Legal only in an explicit bypass context (migrate, seed, platform admin) — confirm and comment it."

grep -Eq 'HasIndex\([^)]*\)[[:space:]]*\.[[:space:]]*IsUnique' "$file" && \
  ! grep -Eq 'HasIndex\([^)]*TenantId' "$file" && \
  warn "declares a unique index that may not include TenantId. Tenant-scoped uniqueness must be (TenantId, ...)."

grep -Eq 'db\.Users|context\.Users|\bUsers\s*\.\s*Where' "$file" && \
  warn "queries the global Users set directly. Tenant member queries go through TenantMemberships joined to Users."

# Always exit 0 — this hook informs, it does not block.
exit 0
