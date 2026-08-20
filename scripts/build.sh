#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
source "$SCRIPT_DIR/dotnet-env.sh"
cd "$SCRIPT_DIR/.."

dotnet build ToiNoMori.Mvs01.slnx \
  --configuration Release \
  --disable-build-servers \
  -m:1 \
  -p:RestoreBuildInParallel=false
