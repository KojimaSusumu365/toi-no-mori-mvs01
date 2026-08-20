#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd -- "$SCRIPT_DIR/.." && pwd)"
EVIDENCE_DIR="${MVS01_EVIDENCE_DIR:-$PROJECT_ROOT/docs/evidence}"

source "$SCRIPT_DIR/dotnet-env.sh"
cd "$PROJECT_ROOT"

"$SCRIPT_DIR/check-test-ids.sh"

dotnet build ToiNoMori.Mvs01.slnx \
    --configuration Release \
    --disable-build-servers \
    -m:1 \
    -p:RestoreBuildInParallel=false

dotnet run \
    --project tests/ToiNoMori.Api.Tests/ToiNoMori.Api.Tests.csproj \
    --configuration Release \
    --no-build

python3 tests/stage6r1/stage6r1_red_tests.py \
    --assert-red \
    --json "$EVIDENCE_DIR/stage6r4-remaining-red-result.json"

if [[ "${MVS01_RUN_POSTGRESQL:-0}" == "1" ]]; then
    "$SCRIPT_DIR/test-postgresql.sh"
else
    echo "PostgreSQL native 10件は未実行です。MVS01_RUN_POSTGRESQL=1で明示実行してください。"
fi
