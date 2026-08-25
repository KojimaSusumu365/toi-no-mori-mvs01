#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"

SUITE_FAILURE=0

if ! "$SCRIPT_DIR/test.sh"; then
  SUITE_FAILURE=1
fi
if ! "$SCRIPT_DIR/test-postgresql.sh"; then
  SUITE_FAILURE=1
fi
if ! "$SCRIPT_DIR/test-disaster-recovery.sh"; then
  SUITE_FAILURE=1
fi

exit "$SUITE_FAILURE"
