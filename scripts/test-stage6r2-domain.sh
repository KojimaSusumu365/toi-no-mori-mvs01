#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd -- "$SCRIPT_DIR/.." && pwd)"
EVIDENCE_DIR="${MVS01_EVIDENCE_DIR:-$PROJECT_ROOT/docs/evidence}"

source "$SCRIPT_DIR/dotnet-env.sh"
cd "$PROJECT_ROOT"

"$SCRIPT_DIR/check-test-ids.sh"

dotnet build tests/ToiNoMori.Domain.Tests/ToiNoMori.Domain.Tests.csproj \
    --configuration Release \
    --disable-build-servers \
    -m:1 \
    -p:RestoreBuildInParallel=false

dotnet run \
    --project tests/ToiNoMori.Domain.Tests/ToiNoMori.Domain.Tests.csproj \
    --configuration Release \
    --no-build

python3 tests/stage6r1/stage6r1_red_tests.py \
    --assert-red \
    --json "$EVIDENCE_DIR/stage6r2-remaining-red-result.json"
