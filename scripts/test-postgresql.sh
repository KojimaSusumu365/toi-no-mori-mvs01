#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd -- "$SCRIPT_DIR/.." && pwd)"
source "$SCRIPT_DIR/dotnet-env.sh"

"$SCRIPT_DIR/build.sh"

POSTGRES_BIN_DIR="${POSTGRES_BIN_DIR:-$PROJECT_ROOT/.tools/postgresql/bin}"
if [[ ! -x "$POSTGRES_BIN_DIR/postgres" \
    || ! -x "$POSTGRES_BIN_DIR/initdb" \
    || ! -x "$POSTGRES_BIN_DIR/psql" ]]; then
    echo "PostgreSQL 18系が見つかりません: $POSTGRES_BIN_DIR" >&2
    echo "POSTGRES_BIN_DIRを指定するか、プロジェクトの.tools/postgresqlへ導入してください。" >&2
    exit 2
fi
if [[ -z "${POSTGRES_RUN_AS+x}" ]]; then
    if [[ "$EUID" -eq 0 ]]; then
        POSTGRES_RUN_AS="nobody"
    else
        POSTGRES_RUN_AS=""
    fi
fi
POSTGRES_DB_USER="${POSTGRES_DB_USER:-${POSTGRES_RUN_AS:-$(id -un)}}"
POSTGRES_APPLICATION_ROLE="${MVS01_POSTGRES_APPLICATION_ROLE:-mvs01_app}"
POSTGRES_MIGRATION_ROLE="${MVS01_POSTGRES_MIGRATION_ROLE:-mvs01_migrator}"
POSTGRES_PLATFORM_WRITER_ROLE="${MVS01_POSTGRES_PLATFORM_AUDIT_WRITER_ROLE:-mvs01_platform_audit_writer}"
POSTGRES_PLATFORM_READER_ROLE="${MVS01_POSTGRES_PLATFORM_AUDIT_READER_ROLE:-mvs01_platform_audit_reader}"
POSTGRES_BYPASS_ROLE="${MVS01_POSTGRES_BYPASS_ROLE:-mvs01_bypass_test}"
POSTGRES_PORT="${MVS01_POSTGRES_PORT:-55432}"
POSTGRES_TEMP="$(mktemp -d /tmp/toi-no-mori-pg.XXXXXX)"
POSTGRES_LOG="$POSTGRES_TEMP/postgresql.log"

postgres_roles=(
    "$POSTGRES_APPLICATION_ROLE"
    "$POSTGRES_MIGRATION_ROLE"
    "$POSTGRES_PLATFORM_WRITER_ROLE"
    "$POSTGRES_PLATFORM_READER_ROLE"
    "$POSTGRES_BYPASS_ROLE")
for postgres_role in "${postgres_roles[@]}"; do
    if [[ ! "$postgres_role" =~ ^[a-z_][a-z0-9_]{0,62}$ ]]; then
        echo "PostgreSQL role名が安全な形式を満たしません。" >&2
        exit 2
    fi
done
if [[ "$(printf '%s\n' "${postgres_roles[@]}" | sort -u | wc -l)" -ne "${#postgres_roles[@]}" ]]; then
    echo "PostgreSQL application/migration/platform audit roleの分離条件を満たしません。" >&2
    exit 2
fi

if [[ -n "$POSTGRES_RUN_AS" ]] \
    && ! runuser -u "$POSTGRES_RUN_AS" -- true >/dev/null 2>&1; then
    echo "この実行環境ではPostgreSQL用の実効ユーザーへ切り替えられません: $POSTGRES_RUN_AS" >&2
    echo "root拒否を解除せず、非root runnerを持つCI/開発環境で実行してください。" >&2
    exit 2
fi

run_postgres() {
    if [[ -n "$POSTGRES_RUN_AS" ]]; then
        runuser -u "$POSTGRES_RUN_AS" -- "$@"
    else
        "$@"
    fi
}

cleanup() {
    if run_postgres "$POSTGRES_BIN_DIR/pg_ctl" -D "$POSTGRES_TEMP/data" status >/dev/null 2>&1; then
        run_postgres "$POSTGRES_BIN_DIR/pg_ctl" -D "$POSTGRES_TEMP/data" -m immediate stop >/dev/null
    fi
    case "$POSTGRES_TEMP" in
        /tmp/toi-no-mori-pg.*) rm -rf -- "$POSTGRES_TEMP" ;;
    esac
}
trap cleanup EXIT

if [[ -n "$POSTGRES_RUN_AS" ]]; then
    chmod 1777 "$POSTGRES_TEMP"
else
    chmod 700 "$POSTGRES_TEMP"
fi
run_postgres "$POSTGRES_BIN_DIR/initdb" \
    -D "$POSTGRES_TEMP/data" \
    --no-locale \
    --encoding=UTF8 \
    --auth-local=trust \
    --auth-host=trust >/dev/null
if ! run_postgres "$POSTGRES_BIN_DIR/pg_ctl" \
    -D "$POSTGRES_TEMP/data" \
    -l "$POSTGRES_LOG" \
    -o "-h 127.0.0.1 -p $POSTGRES_PORT -c unix_socket_directories=" \
    start >/dev/null; then
    sed -n '1,160p' "$POSTGRES_LOG" >&2
    exit 1
fi

run_postgres "$POSTGRES_BIN_DIR/psql" \
    --host=127.0.0.1 \
    --port="$POSTGRES_PORT" \
    --username="$POSTGRES_DB_USER" \
    --dbname=postgres \
    --set=ON_ERROR_STOP=1 \
    --set="application_role=$POSTGRES_APPLICATION_ROLE" \
    --set="migration_role=$POSTGRES_MIGRATION_ROLE" \
    --set="platform_writer_role=$POSTGRES_PLATFORM_WRITER_ROLE" \
    --set="platform_reader_role=$POSTGRES_PLATFORM_READER_ROLE" \
    --set="bypass_role=$POSTGRES_BYPASS_ROLE" \
    <<'SQL' >/dev/null
REVOKE CREATE ON SCHEMA public FROM PUBLIC;

CREATE ROLE :"application_role"
    LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOINHERIT NOREPLICATION NOBYPASSRLS;
CREATE ROLE :"migration_role"
    LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOINHERIT NOREPLICATION NOBYPASSRLS;
CREATE ROLE :"platform_writer_role"
    LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOINHERIT NOREPLICATION NOBYPASSRLS;
CREATE ROLE :"platform_reader_role"
    LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOINHERIT NOREPLICATION NOBYPASSRLS;
CREATE ROLE :"bypass_role"
    LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOINHERIT NOREPLICATION BYPASSRLS;

GRANT CONNECT ON DATABASE postgres
    TO :"application_role", :"migration_role", :"platform_writer_role", :"platform_reader_role", :"bypass_role";
GRANT CREATE ON DATABASE postgres TO :"migration_role";
GRANT USAGE, CREATE ON SCHEMA public TO :"migration_role" WITH GRANT OPTION;
REVOKE CREATE ON SCHEMA public FROM :"application_role";
SQL

export MVS01_TEST_POSTGRES_CONNECTION="Host=127.0.0.1;Port=$POSTGRES_PORT;Username=$POSTGRES_APPLICATION_ROLE;Database=postgres;Pooling=false;Timeout=2;Command Timeout=2;SSL Mode=Disable"
export MVS01_TEST_POSTGRES_MIGRATOR_CONNECTION="Host=127.0.0.1;Port=$POSTGRES_PORT;Username=$POSTGRES_MIGRATION_ROLE;Database=postgres;Pooling=false;Timeout=2;Command Timeout=2;SSL Mode=Disable"
export MVS01_TEST_POSTGRES_PLATFORM_AUDIT_WRITER_CONNECTION="Host=127.0.0.1;Port=$POSTGRES_PORT;Username=$POSTGRES_PLATFORM_WRITER_ROLE;Database=postgres;Pooling=false;Timeout=2;Command Timeout=2;SSL Mode=Disable"
export MVS01_TEST_POSTGRES_PLATFORM_AUDIT_READER_CONNECTION="Host=127.0.0.1;Port=$POSTGRES_PORT;Username=$POSTGRES_PLATFORM_READER_ROLE;Database=postgres;Pooling=false;Timeout=2;Command Timeout=2;SSL Mode=Disable"
export MVS01_TEST_POSTGRES_ADMIN_CONNECTION="Host=127.0.0.1;Port=$POSTGRES_PORT;Username=$POSTGRES_DB_USER;Database=postgres;Pooling=false;Timeout=2;Command Timeout=2;SSL Mode=Disable"
export MVS01_TEST_POSTGRES_BYPASS_CONNECTION="Host=127.0.0.1;Port=$POSTGRES_PORT;Username=$POSTGRES_BYPASS_ROLE;Database=postgres;Pooling=false;Timeout=2;Command Timeout=2;SSL Mode=Disable"
export MVS01_TEST_PG_CTL="$POSTGRES_BIN_DIR/pg_ctl"
export MVS01_TEST_PG_DATA="$POSTGRES_TEMP/data"
export MVS01_TEST_PG_RUN_AS="$POSTGRES_RUN_AS"

dotnet run --project "$PROJECT_ROOT/tests/ToiNoMori.PostgreSql.Tests/ToiNoMori.PostgreSql.Tests.csproj" \
    --configuration Release \
    --no-build
