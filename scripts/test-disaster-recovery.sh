#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd -- "$SCRIPT_DIR/.." && pwd)"
source "$SCRIPT_DIR/dotnet-env.sh"

"$SCRIPT_DIR/build.sh"

POSTGRES_BIN_DIR="${POSTGRES_BIN_DIR:-$PROJECT_ROOT/.tools/postgresql/bin}"
if [[ ! -x "$POSTGRES_BIN_DIR/postgres" || ! -x "$POSTGRES_BIN_DIR/initdb" ]]; then
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
POSTGRES_APPLICATION_ROLE="${MVS01_DR_POSTGRES_APPLICATION_ROLE:-mvs01_dr_app}"
POSTGRES_PLATFORM_WRITER_ROLE="${MVS01_DR_POSTGRES_PLATFORM_WRITER_ROLE:-mvs01_dr_platform_writer}"
POSTGRES_PLATFORM_READER_ROLE="${MVS01_DR_POSTGRES_PLATFORM_READER_ROLE:-mvs01_dr_platform_reader}"
PRIMARY_PORT="${MVS01_DR_PRIMARY_PORT:-55432}"
RECOVERY_PORT="${MVS01_DR_RECOVERY_PORT:-55433}"
API_PORT="${MVS01_DR_API_PORT:-5083}"
MAX_RPO_SECONDS="${MVS01_DR_MAX_RPO_SECONDS:-3600}"
MAX_RTO_SECONDS="${MVS01_DR_MAX_RTO_SECONDS:-14400}"
DR_TEMP="$(mktemp -d /tmp/toi-no-mori-dr-drill.XXXXXX)"
API_PID=""

service_roles=(
    "$POSTGRES_DB_USER"
    "$POSTGRES_APPLICATION_ROLE"
    "$POSTGRES_PLATFORM_WRITER_ROLE"
    "$POSTGRES_PLATFORM_READER_ROLE")
for service_role in "${service_roles[@]}"; do
    if [[ ! "$service_role" =~ ^[a-z_][a-z0-9_]{0,62}$ ]]; then
        echo "DR PostgreSQL role名が安全な形式を満たしません。" >&2
        exit 2
    fi
done
if [[ "$(printf '%s\n' "${service_roles[@]}" | sort -u | wc -l)" -ne "${#service_roles[@]}" ]]; then
    echo "DR PostgreSQL migration/application/platform audit roleの分離条件を満たしません。" >&2
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

stop_api() {
    if [[ -n "$API_PID" ]] && kill -0 "$API_PID" >/dev/null 2>&1; then
        kill "$API_PID"
        wait "$API_PID" 2>/dev/null || true
    fi
    API_PID=""
}

stop_cluster() {
    local data_directory="$1"
    if [[ -d "$data_directory" ]] \
        && run_postgres "$POSTGRES_BIN_DIR/pg_ctl" -D "$data_directory" status >/dev/null 2>&1; then
        run_postgres "$POSTGRES_BIN_DIR/pg_ctl" \
            -D "$data_directory" \
            -m immediate \
            stop >/dev/null
    fi
}

cleanup() {
    stop_api
    stop_cluster "$DR_TEMP/primary"
    stop_cluster "$DR_TEMP/recovery"
    case "$DR_TEMP" in
        /tmp/toi-no-mori-dr-drill.*) rm -rf -- "$DR_TEMP" ;;
    esac
}
trap cleanup EXIT

if [[ -n "$POSTGRES_RUN_AS" ]]; then
    chmod 1777 "$DR_TEMP"
else
    chmod 700 "$DR_TEMP"
fi

start_cluster() {
    local name="$1"
    local port="$2"
    local data_directory="$DR_TEMP/$name"
    local log_file="$DR_TEMP/$name.log"

    run_postgres "$POSTGRES_BIN_DIR/initdb" \
        -D "$data_directory" \
        --username="$POSTGRES_DB_USER" \
        --no-locale \
        --encoding=UTF8 \
        --auth-local=trust \
        --auth-host=trust >/dev/null
    if ! run_postgres "$POSTGRES_BIN_DIR/pg_ctl" \
        -D "$data_directory" \
        -l "$log_file" \
        -o "-h 127.0.0.1 -p $port -c unix_socket_directories=" \
        start >/dev/null; then
        sed -n '1,160p' "$log_file" >&2
        exit 1
    fi
}

create_service_roles() {
    local port="$1"
    run_postgres "$POSTGRES_BIN_DIR/psql" \
        --host=127.0.0.1 \
        --port="$port" \
        --username="$POSTGRES_DB_USER" \
        --dbname=postgres \
        --no-psqlrc \
        --set=ON_ERROR_STOP=1 \
        --set="application_role=$POSTGRES_APPLICATION_ROLE" \
        --set="platform_writer_role=$POSTGRES_PLATFORM_WRITER_ROLE" \
        --set="platform_reader_role=$POSTGRES_PLATFORM_READER_ROLE" \
        <<'SQL' >/dev/null
REVOKE CREATE ON SCHEMA public FROM PUBLIC;
CREATE ROLE :"application_role"
    LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOINHERIT NOREPLICATION NOBYPASSRLS;
CREATE ROLE :"platform_writer_role"
    LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOINHERIT NOREPLICATION NOBYPASSRLS;
CREATE ROLE :"platform_reader_role"
    LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOINHERIT NOREPLICATION NOBYPASSRLS;
GRANT CONNECT ON DATABASE postgres
    TO :"application_role", :"platform_writer_role", :"platform_reader_role";
REVOKE CREATE ON SCHEMA public FROM :"application_role";
SQL
}

wait_for_url() {
    local url="$1"
    for _ in $(seq 1 120); do
        if curl --fail --silent --show-error "$url" >/dev/null 2>&1; then
            return 0
        fi
        sleep 0.25
    done
    return 1
}

start_api() {
    local port="$1"
    local database_port="$2"
    local log_file="$3"
    local application_connection_string="Host=127.0.0.1;Port=$database_port;Username=$POSTGRES_APPLICATION_ROLE;Database=postgres;Pooling=false;Timeout=2;Command Timeout=2;SSL Mode=Disable"
    local migration_connection_string="Host=127.0.0.1;Port=$database_port;Username=$POSTGRES_DB_USER;Database=postgres;Pooling=false;Timeout=2;Command Timeout=2;SSL Mode=Disable"
    local platform_writer_connection_string="Host=127.0.0.1;Port=$database_port;Username=$POSTGRES_PLATFORM_WRITER_ROLE;Database=postgres;Pooling=false;Timeout=2;Command Timeout=2;SSL Mode=Disable"
    local platform_reader_connection_string="Host=127.0.0.1;Port=$database_port;Username=$POSTGRES_PLATFORM_READER_ROLE;Database=postgres;Pooling=false;Timeout=2;Command Timeout=2;SSL Mode=Disable"

    env \
        ASPNETCORE_ENVIRONMENT=Testing \
        ASPNETCORE_URLS="http://127.0.0.1:$port" \
        Persistence__Provider=PostgreSql \
        ConnectionStrings__PostgreSql="$application_connection_string" \
        ConnectionStrings__PostgreSqlMigrator="$migration_connection_string" \
        ConnectionStrings__PostgreSqlPlatformAuditWriter="$platform_writer_connection_string" \
        ConnectionStrings__PostgreSqlPlatformAuditReader="$platform_reader_connection_string" \
        Audit__PartitionHashKey=MDEyMzQ1Njc4OWFiY2RlZjAxMjM0NTY3ODlhYmNkZWY= \
        dotnet "$PROJECT_ROOT/src/ToiNoMori.Api/bin/Release/net10.0/ToiNoMori.Api.dll" \
        >"$log_file" 2>&1 &
    API_PID="$!"
    if ! wait_for_url "http://127.0.0.1:$port/health/ready"; then
        sed -n '1,200p' "$log_file" >&2
        exit 1
    fi
}

psql_primary=(
    "$POSTGRES_BIN_DIR/psql"
    --host=127.0.0.1
    --port="$PRIMARY_PORT"
    --username="$POSTGRES_DB_USER"
    --dbname=postgres
    --no-psqlrc
    --set=ON_ERROR_STOP=1
)

psql_recovery=(
    "$POSTGRES_BIN_DIR/psql"
    --host=127.0.0.1
    --port="$RECOVERY_PORT"
    --username="$POSTGRES_DB_USER"
    --dbname=postgres
    --no-psqlrc
    --tuples-only
    --no-align
    --set=ON_ERROR_STOP=1
)

openssl req -x509 \
    -newkey rsa:3072 \
    -sha256 \
    -nodes \
    -days 2 \
    -subj '/CN=ToiNoMori DR Test Signer' \
    -addext 'basicConstraints=critical,CA:TRUE' \
    -addext 'keyUsage=critical,digitalSignature,keyCertSign' \
    -keyout "$DR_TEMP/signer.key" \
    -out "$DR_TEMP/signer.crt" >/dev/null 2>&1
openssl req -x509 \
    -newkey rsa:3072 \
    -sha256 \
    -nodes \
    -days 2 \
    -subj '/CN=ToiNoMori DR Test Recovery' \
    -addext 'keyUsage=critical,keyEncipherment,dataEncipherment' \
    -keyout "$DR_TEMP/recovery.key" \
    -out "$DR_TEMP/recovery.crt" >/dev/null 2>&1
chmod 600 "$DR_TEMP/signer.key" "$DR_TEMP/recovery.key"

start_cluster primary "$PRIMARY_PORT"
create_service_roles "$PRIMARY_PORT"
start_api "$API_PORT" "$PRIMARY_PORT" "$DR_TEMP/primary-api.log"
stop_api

"${psql_primary[@]}" >/dev/null <<'SQL'
BEGIN;
SET CONSTRAINTS ALL DEFERRED;
INSERT INTO questions (
    id, tenant_id, title, body, tags, status, version, owner_subject,
    created_at, updated_at, published_at, review_reason, withdrawal_reason,
    approved_version, approved_by, published_revision_id)
VALUES (
    '00000000-0000-0000-0000-000000000030',
    '7b48e239-07ef-4b34-a1fb-7f4fc7ff1673',
    'DR sentinel publication',
    'This record proves isolated disaster recovery.',
    ARRAY['dr', 'recovery'],
    'PUBLISHED',
    3,
    'dr-owner',
    clock_timestamp(),
    clock_timestamp(),
    clock_timestamp(),
    NULL,
    NULL,
    3,
    'dr-reviewer',
    '00000000-0000-0000-0000-000000000030');

INSERT INTO question_revisions (
    tenant_id, id, question_id, version, title, body, tags, status,
    owner_subject, created_at, recorded_at, published_at, review_reason,
    withdrawal_reason, approved_version, approved_by)
VALUES (
    '7b48e239-07ef-4b34-a1fb-7f4fc7ff1673',
    '00000000-0000-0000-0000-000000000030',
    '00000000-0000-0000-0000-000000000030',
    3,
    'DR sentinel publication',
    'This record proves isolated disaster recovery.',
    ARRAY['dr', 'recovery'],
    'PUBLISHED',
    'dr-owner',
    clock_timestamp(),
    clock_timestamp(),
    clock_timestamp(),
    NULL,
    NULL,
    3,
    'dr-reviewer');

INSERT INTO audit_events (
    id, tenant_id, actor_subject, target_id, action, result, correlation_id, occurred_at)
VALUES (
    '00000000-0000-0000-0000-000000000031',
    '7b48e239-07ef-4b34-a1fb-7f4fc7ff1673',
    'dr-reviewer',
    '00000000-0000-0000-0000-000000000030',
    'question.approve',
    'success',
    'dr-drill-seed',
    clock_timestamp());
COMMIT;
SQL

mkdir -p "$DR_TEMP/backups"
backup_path="$(env \
    POSTGRES_BIN_DIR="$POSTGRES_BIN_DIR" \
    MVS01_SOURCE_PGHOST=127.0.0.1 \
    MVS01_SOURCE_PGPORT="$PRIMARY_PORT" \
    MVS01_SOURCE_PGUSER="$POSTGRES_DB_USER" \
    MVS01_SOURCE_PGDATABASE=postgres \
    MVS01_DR_OUTPUT_DIR="$DR_TEMP/backups" \
    MVS01_DR_SIGNER_CERT="$DR_TEMP/signer.crt" \
    MVS01_DR_SIGNER_KEY="$DR_TEMP/signer.key" \
    MVS01_DR_RECIPIENT_CERT="$DR_TEMP/recovery.crt" \
    "$SCRIPT_DIR/dr/create-encrypted-backup.sh")"
metadata_path="${backup_path%.p7m}.metadata.json"

mkdir -p "$DR_TEMP/validation"
validation_report="$(env \
    POSTGRES_BIN_DIR="$POSTGRES_BIN_DIR" \
    MVS01_DR_BACKUP_FILE="$backup_path" \
    MVS01_DR_RESTORE_DIR="$DR_TEMP/validation" \
    MVS01_DR_RECIPIENT_CERT="$DR_TEMP/recovery.crt" \
    MVS01_DR_RECIPIENT_KEY="$DR_TEMP/recovery.key" \
    MVS01_DR_TRUSTED_SIGNER_CERT="$DR_TEMP/signer.crt" \
    "$SCRIPT_DIR/dr/restore-encrypted-backup.sh")"

printf '# ToiNoMori disaster-recovery specification tests\n'
printf '1..4\n'
passed=0
failed=0

if [[ -f "$backup_path" \
    && -f "$metadata_path" \
    && -f "$validation_report" \
    && "$(jq -r '.format' "$DR_TEMP/validation/manifest.json")" == "toi-no-mori-dr-backup-v1" ]] \
    && ! rg -a -q 'DR sentinel publication|This record proves isolated disaster recovery' "$backup_path"; then
    printf 'ok 1 - TC-ACC-MVS01-030 [REQ-MVS01-DR-002] 署名済みAES-256-GCM暗号化バックアップ\n'
    passed=$((passed + 1))
else
    printf 'not ok 1 - TC-ACC-MVS01-030 [REQ-MVS01-DR-002] 署名済みAES-256-GCM暗号化バックアップ\n'
    failed=$((failed + 1))
fi

tampered_backup="$DR_TEMP/tampered.p7m"
cp -- "$backup_path" "$tampered_backup"
perl -0777 -pi -e 'substr($_, 128, 1) = chr(ord(substr($_, 128, 1)) ^ 1)' "$tampered_backup"
mkdir -p "$DR_TEMP/tampered-restore"
if ! env \
    POSTGRES_BIN_DIR="$POSTGRES_BIN_DIR" \
    MVS01_DR_BACKUP_FILE="$tampered_backup" \
    MVS01_DR_RESTORE_DIR="$DR_TEMP/tampered-restore" \
    MVS01_DR_RECIPIENT_CERT="$DR_TEMP/recovery.crt" \
    MVS01_DR_RECIPIENT_KEY="$DR_TEMP/recovery.key" \
    MVS01_DR_TRUSTED_SIGNER_CERT="$DR_TEMP/signer.crt" \
    "$SCRIPT_DIR/dr/restore-encrypted-backup.sh" >/dev/null 2>&1; then
    printf 'ok 2 - TC-ACC-MVS01-031 [REQ-MVS01-DR-003] 暗号化バックアップの改ざんを拒否\n'
    passed=$((passed + 1))
else
    printf 'not ok 2 - TC-ACC-MVS01-031 [REQ-MVS01-DR-003] 暗号化バックアップの改ざんを拒否\n'
    failed=$((failed + 1))
fi

snapshot_started_at="$(jq -r '.snapshotStartedAtUtc' "$DR_TEMP/validation/manifest.json")"
disaster_epoch="$(date -u +%s)"
stop_cluster "$DR_TEMP/primary"
start_cluster recovery "$RECOVERY_PORT"
create_service_roles "$RECOVERY_PORT"

mkdir -p "$DR_TEMP/recovered-payload"
recovery_report="$(env \
    POSTGRES_BIN_DIR="$POSTGRES_BIN_DIR" \
    MVS01_DR_BACKUP_FILE="$backup_path" \
    MVS01_DR_RESTORE_DIR="$DR_TEMP/recovered-payload" \
    MVS01_DR_RECIPIENT_CERT="$DR_TEMP/recovery.crt" \
    MVS01_DR_RECIPIENT_KEY="$DR_TEMP/recovery.key" \
    MVS01_DR_TRUSTED_SIGNER_CERT="$DR_TEMP/signer.crt" \
    MVS01_TARGET_PGHOST=127.0.0.1 \
    MVS01_TARGET_PGPORT="$RECOVERY_PORT" \
    MVS01_TARGET_PGUSER="$POSTGRES_DB_USER" \
    MVS01_TARGET_PGDATABASE=postgres \
    "$SCRIPT_DIR/dr/restore-encrypted-backup.sh")"

start_api "$API_PORT" "$RECOVERY_PORT" "$DR_TEMP/recovery-api.log"
public_json="$(curl --fail --silent --show-error \
    "http://127.0.0.1:$API_PORT/api/public/questions/00000000-0000-0000-0000-000000000030")"
recovered_audit_count="$("${psql_recovery[@]}" \
    --command="SELECT count(*) FROM audit_events WHERE target_id = '00000000-0000-0000-0000-000000000030' AND action = 'question.approve' AND result = 'success';")"
recovery_success_epoch="$(date -u +%s)"

if [[ "$(jq -r '.id' <<<"$public_json")" == "00000000-0000-0000-0000-000000000030" \
    && "$(jq -r '.title' <<<"$public_json")" == "DR sentinel publication" \
    && "$recovered_audit_count" == "1" \
    && "$(jq -r '.questionCount' "$recovery_report")" == "1" \
    && "$(jq -r '.auditCount' "$recovery_report")" == "1" ]]; then
    printf 'ok 3 - TC-ACC-MVS01-032 [REQ-MVS01-DR-004] 別DBへの隔離復元とAPI・監査整合性\n'
    passed=$((passed + 1))
else
    printf 'not ok 3 - TC-ACC-MVS01-032 [REQ-MVS01-DR-004] 別DBへの隔離復元とAPI・監査整合性\n'
    failed=$((failed + 1))
fi

snapshot_epoch="$(date -u -d "$snapshot_started_at" +%s)"
rpo_seconds=$((disaster_epoch - snapshot_epoch))
rto_seconds=$((recovery_success_epoch - disaster_epoch))
if (( rpo_seconds >= 0 \
    && rpo_seconds <= MAX_RPO_SECONDS \
    && rto_seconds >= 0 \
    && rto_seconds <= MAX_RTO_SECONDS )); then
    printf 'ok 4 - TC-ACC-MVS01-033 [REQ-MVS01-DR-005] RPO・RTOを計測し暫定目標内と判定\n'
    passed=$((passed + 1))
else
    printf 'not ok 4 - TC-ACC-MVS01-033 [REQ-MVS01-DR-005] RPO・RTOを計測し暫定目標内と判定\n'
    failed=$((failed + 1))
fi

printf '# metrics: rpo_seconds=%s rto_seconds=%s max_rpo_seconds=%s max_rto_seconds=%s\n' \
    "$rpo_seconds" "$rto_seconds" "$MAX_RPO_SECONDS" "$MAX_RTO_SECONDS"
printf '# result: %s passed; %s failed; 4 total\n' "$passed" "$failed"

if [[ -n "${MVS01_DR_EVIDENCE_DIR:-}" ]]; then
    mkdir -p -- "$MVS01_DR_EVIDENCE_DIR"
    jq -n \
        --arg format "toi-no-mori-dr-drill-evidence-v1" \
        --arg executedAtUtc "$(date -u +%Y-%m-%dT%H:%M:%SZ)" \
        --arg sourceRole "ishikari-primary-simulated" \
        --arg recoveryRole "tokyo-recovery-simulated" \
        --argjson rpoSeconds "$rpo_seconds" \
        --argjson rtoSeconds "$rto_seconds" \
        --argjson maxRpoSeconds "$MAX_RPO_SECONDS" \
        --argjson maxRtoSeconds "$MAX_RTO_SECONDS" \
        --argjson passed "$passed" \
        --argjson failed "$failed" \
        '{
            format: $format,
            executedAtUtc: $executedAtUtc,
            sourceRole: $sourceRole,
            recoveryRole: $recoveryRole,
            rpoSeconds: $rpoSeconds,
            rtoSeconds: $rtoSeconds,
            maxRpoSeconds: $maxRpoSeconds,
            maxRtoSeconds: $maxRtoSeconds,
            testsPassed: $passed,
            testsFailed: $failed,
            scope: "local isolated process drill; not a cloud region failover"
        }' >"$MVS01_DR_EVIDENCE_DIR/dr-drill-latest.json"
fi

stop_api
if (( failed > 0 )); then
    exit 1
fi
