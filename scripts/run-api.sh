#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
source "$SCRIPT_DIR/dotnet-env.sh"
cd "$SCRIPT_DIR/.."

dotnet run --project src/ToiNoMori.Api/ToiNoMori.Api.csproj
