#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd -- "$SCRIPT_DIR/.." && pwd)"
source "$SCRIPT_DIR/dotnet-env.sh"

POSTGRES_BIN_DIR="${POSTGRES_BIN_DIR:-$PROJECT_ROOT/.tools/postgresql/bin}"
if [[ ! -x "$POSTGRES_BIN_DIR/postgres" ]]; then
    echo "PostgreSQLが見つかりません: $POSTGRES_BIN_DIR" >&2
    exit 2
fi

dotnet_version="$(dotnet --version)"
postgres_version="$($POSTGRES_BIN_DIR/postgres --version)"
if [[ "$dotnet_version" != "10.0.400" ]]; then
    echo "想定外の.NET SDK: $dotnet_version" >&2
    exit 3
fi
if [[ "$postgres_version" != "postgres (PostgreSQL) 18.6" ]]; then
    echo "想定外のPostgreSQL: $postgres_version" >&2
    exit 3
fi

for command_name in postgres initdb pg_ctl psql pg_dump pg_restore pg_config; do
    "$POSTGRES_BIN_DIR/$command_name" --version >/dev/null
done
if ldd "$POSTGRES_BIN_DIR/postgres" "$POSTGRES_BIN_DIR/psql" | grep -q 'not found'; then
    echo "PostgreSQLの共有libraryが不足しています。" >&2
    exit 4
fi

printf '.NET SDK: %s\n' "$dotnet_version"
printf 'PostgreSQL: %s\n' "${postgres_version#postgres (PostgreSQL) }"
printf 'DOTNET_ROOT: %s\n' "$DOTNET_ROOT"
printf 'POSTGRES_BIN_DIR: %s\n' "$POSTGRES_BIN_DIR"
