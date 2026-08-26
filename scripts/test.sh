#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
source "$SCRIPT_DIR/dotnet-env.sh"
cd "$SCRIPT_DIR/.."

"$SCRIPT_DIR/build.sh"

SUITE_FAILURE=0

if ! dotnet run \
  --project tests/ToiNoMori.Domain.Tests/ToiNoMori.Domain.Tests.csproj \
  --configuration Release \
  --no-build; then
  SUITE_FAILURE=1
fi

if ! dotnet run \
  --project tests/ToiNoMori.Api.Tests/ToiNoMori.Api.Tests.csproj \
  --configuration Release \
  --no-build; then
  SUITE_FAILURE=1
fi

if ! dotnet run \
  --project tests/ToiNoMori.Mobile.Tests/ToiNoMori.Mobile.Tests.csproj \
  --configuration Release \
  --no-build; then
  SUITE_FAILURE=1
fi

if ! dotnet run \
  --project tests/ToiNoMori.OidcE2e.Tests/ToiNoMori.OidcE2e.Tests.csproj \
  --configuration Release \
  --no-build; then
  SUITE_FAILURE=1
fi

if ! dotnet run \
  --project tests/ToiNoMori.TownReadiness.Tests/ToiNoMori.TownReadiness.Tests.csproj \
  --configuration Release \
  --no-build; then
  SUITE_FAILURE=1
fi

exit "$SUITE_FAILURE"
