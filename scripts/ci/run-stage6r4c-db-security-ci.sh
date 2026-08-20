#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd -- "$SCRIPT_DIR/../.." && pwd)"
EVIDENCE_DIR="${MVS01_CI_EVIDENCE_DIR:-$PROJECT_ROOT/artifacts/stage6r4c}"
LOG_PATH="$EVIDENCE_DIR/stage6r4c-nonroot-postgresql.log"
JSON_PATH="$EVIDENCE_DIR/stage6r4c-nonroot-postgresql-result.json"
SUMMARY_PATH="$EVIDENCE_DIR/stage6r4c-nonroot-postgresql-summary.md"
STARTED_AT_UTC="$(date -u +'%Y-%m-%dT%H:%M:%SZ')"
RUNNER_UID="$(id -u)"
RUNNER_USER="$(id -un)"

mkdir -p "$EVIDENCE_DIR"
: > "$LOG_PATH"

if [[ "$RUNNER_UID" -eq 0 ]]; then
    {
        echo "Stage 6R-4C preflight failed: PostgreSQL CI must run as a non-root user."
        echo "The PostgreSQL root guard remains enabled; the native suite was not started."
    } | tee "$LOG_PATH" >&2
    GATE_EXIT_CODE=2
else
    set +e
    "$PROJECT_ROOT/scripts/test-stage6r4-db-security.sh" 2>&1 | tee "$LOG_PATH"
    GATE_EXIT_CODE=${PIPESTATUS[0]}
    set -e
fi

FINISHED_AT_UTC="$(date -u +'%Y-%m-%dT%H:%M:%SZ')"
DOTNET_VERSION="unavailable"
POSTGRESQL_VERSION="unavailable"

if source "$PROJECT_ROOT/scripts/dotnet-env.sh" >/dev/null 2>&1; then
    DOTNET_VERSION="$(dotnet --version 2>/dev/null || printf 'unavailable')"
fi
if [[ -x "$PROJECT_ROOT/.tools/postgresql/bin/postgres" ]]; then
    POSTGRESQL_VERSION="$($PROJECT_ROOT/.tools/postgresql/bin/postgres --version 2>/dev/null || printf 'unavailable')"
fi

set +e
python3 "$SCRIPT_DIR/write-stage6r4c-evidence.py" \
    --log "$LOG_PATH" \
    --json "$JSON_PATH" \
    --summary "$SUMMARY_PATH" \
    --started-at "$STARTED_AT_UTC" \
    --finished-at "$FINISHED_AT_UTC" \
    --uid "$RUNNER_UID" \
    --user "$RUNNER_USER" \
    --gate-exit-code "$GATE_EXIT_CODE" \
    --execution-mode native \
    --dotnet-version "$DOTNET_VERSION" \
    --postgresql-version "$POSTGRESQL_VERSION"
EVIDENCE_EXIT_CODE=$?
set -e

if [[ "$GATE_EXIT_CODE" -ne 0 ]]; then
    exit "$GATE_EXIT_CODE"
fi
exit "$EVIDENCE_EXIT_CODE"
