#!/usr/bin/env bash
set -euo pipefail

MVS_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

if command -v dotnet >/dev/null 2>&1; then
  DOTNET_BIN="$(command -v dotnet)"
elif [[ -x "$MVS_ROOT/.tools/dotnet/dotnet" ]]; then
  DOTNET_BIN="$MVS_ROOT/.tools/dotnet/dotnet"
elif [[ -x "/tmp/toi-no-mori-dotnet-partial-10.0.400/dotnet" ]]; then
  DOTNET_BIN="/tmp/toi-no-mori-dotnet-partial-10.0.400/dotnet"
else
  echo ".NET SDK 10.0.400 が見つかりません。global.json に一致するSDKを導入してください。" >&2
  exit 2
fi

export DOTNET_ROOT="$(cd "$(dirname "$DOTNET_BIN")" && pwd)"
export PATH="$DOTNET_ROOT:$PATH"
export DOTNET_CLI_HOME="$MVS_ROOT/.dotnet-cli-home"
export NUGET_PACKAGES="$MVS_ROOT/.nuget-packages"
export DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1
export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_NOLOGO=1
mkdir -p "$DOTNET_CLI_HOME" "$NUGET_PACKAGES"
