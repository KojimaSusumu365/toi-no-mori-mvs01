#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd -- "$SCRIPT_DIR/.." && pwd)"
EVIDENCE_DIR="${MVS01_EVIDENCE_DIR:-$PROJECT_ROOT/docs/evidence}"

python3 "$PROJECT_ROOT/tests/stage6r1/stage6r1_red_tests.py" \
    --assert-red \
    --json "$EVIDENCE_DIR/stage6r1-red-result.json"
