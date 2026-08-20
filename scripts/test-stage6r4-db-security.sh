#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd -- "$SCRIPT_DIR/.." && pwd)"

source "$SCRIPT_DIR/dotnet-env.sh"
cd "$PROJECT_ROOT"

"$SCRIPT_DIR/check-test-ids.sh"
"$SCRIPT_DIR/build.sh"

dotnet run \
    --project tests/ToiNoMori.Api.Tests/ToiNoMori.Api.Tests.csproj \
    --configuration Release \
    --no-build

"$SCRIPT_DIR/test-postgresql.sh"
