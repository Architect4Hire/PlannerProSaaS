#!/usr/bin/env bash
# PreToolUse guard: deny writes containing secret-shaped strings. exit 2 = deny.
# Everything here should come from Aspire-injected config, user-secrets, or environment
# variables — never a literal in source.
set -euo pipefail
payload="$(cat)"
patterns='(sk-[A-Za-z0-9_-]{16,}|AKIA[0-9A-Z]{16}|-----BEGIN [A-Z ]*PRIVATE KEY-----|(postgres|postgresql|redis|amqp)://[^:@/]+:[^@/]+@|Endpoint=sb://[^;]+;SharedAccessKey[A-Za-z]*=[^;]+|DefaultEndpointsProtocol=[^;]*;.*AccountKey=[^;]+|"(SigningKey|ClientSecret|Password|ApiKey)"[[:space:]]*:[[:space:]]*"[^"]{1,}"|password[[:space:]]*=[[:space:]]*["'\''][^"'\'' ]{6,})'
if printf '%s' "$payload" | grep -Eiq "$patterns"; then
 echo "secret-guard: blocked a write with a secret-shaped string (DB/Redis/Service Bus/Storage credential, signing key, or password). Use Aspire-injected config, user-secrets in dev, and environment variables in production — never a literal." >&2
 exit 2
fi
exit 0
