#!/usr/bin/env bash
set -euo pipefail

umask 077

require_variable() {
    local name="$1"
    if [[ -z "${!name:-}" ]]; then
        echo "Required environment variable is missing: $name" >&2
        exit 2
    fi
}

for name in \
    MVS01_DR_BACKUP_FILE \
    MVS01_DR_RESTORE_DIR \
    MVS01_DR_RECIPIENT_CERT \
    MVS01_DR_RECIPIENT_KEY \
    MVS01_DR_TRUSTED_SIGNER_CERT; do
    require_variable "$name"
done

POSTGRES_BIN_DIR="${POSTGRES_BIN_DIR:-/usr/bin}"
OPENSSL_BIN="${OPENSSL_BIN:-openssl}"

for executable in \
    "$OPENSSL_BIN" \
    jq \
    sha256sum \
    tar; do
    if ! command -v "$executable" >/dev/null 2>&1; then
        echo "Required executable was not found: $executable" >&2
        exit 2
    fi
done

for readable_file in \
    "$MVS01_DR_BACKUP_FILE" \
    "$MVS01_DR_RECIPIENT_CERT" \
    "$MVS01_DR_RECIPIENT_KEY" \
    "$MVS01_DR_TRUSTED_SIGNER_CERT"; do
    if [[ ! -r "$readable_file" ]]; then
        echo "Required recovery material is not readable: $readable_file" >&2
        exit 2
    fi
done

mkdir -p -- "$MVS01_DR_RESTORE_DIR"
chmod 700 "$MVS01_DR_RESTORE_DIR"
if find "$MVS01_DR_RESTORE_DIR" -mindepth 1 -maxdepth 1 -print -quit | grep -q .; then
    echo "MVS01_DR_RESTORE_DIR must be empty." >&2
    exit 2
fi

work_directory="$(mktemp -d /tmp/toi-no-mori-restore.XXXXXX)"
cleanup() {
    case "$work_directory" in
        /tmp/toi-no-mori-restore.*) rm -rf -- "$work_directory" ;;
    esac
}
trap cleanup EXIT

decrypt_arguments=(
    cms -decrypt -binary
    -inform DER
    -in "$MVS01_DR_BACKUP_FILE"
    -recip "$MVS01_DR_RECIPIENT_CERT"
    -inkey "$MVS01_DR_RECIPIENT_KEY"
    -out "$work_directory/payload.signed.cms"
)
if [[ -n "${MVS01_DR_RECIPIENT_KEY_PASS_FILE:-}" ]]; then
    decrypt_arguments+=( -passin "file:$MVS01_DR_RECIPIENT_KEY_PASS_FILE" )
fi
"$OPENSSL_BIN" "${decrypt_arguments[@]}"

"$OPENSSL_BIN" cms -verify \
    -binary \
    -inform DER \
    -in "$work_directory/payload.signed.cms" \
    -CAfile "$MVS01_DR_TRUSTED_SIGNER_CERT" \
    -no-CApath \
    -no-CAstore \
    -purpose any \
    -verify_retcode \
    -cades \
    -out "$work_directory/payload.tar" >/dev/null

mapfile -t archive_entries < <(tar -tf "$work_directory/payload.tar")
if [[ "${#archive_entries[@]}" -ne 2 \
    || "${archive_entries[0]}" != "manifest.json" \
    || "${archive_entries[1]}" != "database.dump" ]]; then
    echo "Backup payload contains unexpected archive entries." >&2
    exit 4
fi

tar \
    --extract \
    --file "$work_directory/payload.tar" \
    --directory "$MVS01_DR_RESTORE_DIR" \
    --no-same-owner \
    --no-same-permissions

manifest="$MVS01_DR_RESTORE_DIR/manifest.json"
dump_file="$MVS01_DR_RESTORE_DIR/database.dump"
if [[ "$(jq -r '.format' "$manifest")" != "toi-no-mori-dr-backup-v1" ]]; then
    echo "Unsupported backup manifest format." >&2
    exit 4
fi

expected_sha256="$(jq -r '.dumpSha256' "$manifest")"
actual_sha256="$(sha256sum "$dump_file" | awk '{print $1}')"
if [[ ! "$expected_sha256" =~ ^[0-9a-f]{64}$ || "$actual_sha256" != "$expected_sha256" ]]; then
    echo "Database dump integrity check failed." >&2
    exit 4
fi

question_count="null"
audit_count="null"
migration_count="null"
platform_security_event_count="null"
latest_migration_version=""
fk_published_revision_same_question=false
platform_security_events=false
if [[ -n "${MVS01_TARGET_PGHOST:-}" ]]; then
    for name in \
        MVS01_TARGET_PGHOST \
        MVS01_TARGET_PGPORT \
        MVS01_TARGET_PGUSER \
        MVS01_TARGET_PGDATABASE; do
        require_variable "$name"
    done
    for executable in \
        "$POSTGRES_BIN_DIR/pg_restore" \
        "$POSTGRES_BIN_DIR/psql"; do
        if [[ ! -x "$executable" ]]; then
            echo "Required PostgreSQL recovery executable was not found: $executable" >&2
            exit 2
        fi
    done
    if [[ -n "${MVS01_TARGET_PGPASSFILE:-}" ]]; then
        if [[ ! -r "$MVS01_TARGET_PGPASSFILE" ]]; then
            echo "MVS01_TARGET_PGPASSFILE is not readable." >&2
            exit 2
        fi
        export PGPASSFILE="$MVS01_TARGET_PGPASSFILE"
    fi

    "$POSTGRES_BIN_DIR/pg_restore" \
        --host="$MVS01_TARGET_PGHOST" \
        --port="$MVS01_TARGET_PGPORT" \
        --username="$MVS01_TARGET_PGUSER" \
        --dbname="$MVS01_TARGET_PGDATABASE" \
        --exit-on-error \
        --no-owner \
        --no-privileges \
        "$dump_file"

    psql_arguments=(
        --host="$MVS01_TARGET_PGHOST"
        --port="$MVS01_TARGET_PGPORT"
        --username="$MVS01_TARGET_PGUSER"
        --dbname="$MVS01_TARGET_PGDATABASE"
        --no-psqlrc
        --tuples-only
        --no-align
        --set=ON_ERROR_STOP=1
    )
    question_count="$("$POSTGRES_BIN_DIR/psql" "${psql_arguments[@]}" --command='SELECT count(*) FROM questions;')"
    audit_count="$("$POSTGRES_BIN_DIR/psql" "${psql_arguments[@]}" --command='SELECT count(*) FROM audit_events;')"
    migration_count="$("$POSTGRES_BIN_DIR/psql" "${psql_arguments[@]}" --command='SELECT count(*) FROM schema_migrations;')"
    platform_security_event_count="$("$POSTGRES_BIN_DIR/psql" "${psql_arguments[@]}" --command='SELECT count(*) FROM platform_security_events;')"
    latest_migration_version="$("$POSTGRES_BIN_DIR/psql" "${psql_arguments[@]}" --command='SELECT version FROM schema_migrations ORDER BY version DESC LIMIT 1;')"
    if [[ "$("$POSTGRES_BIN_DIR/psql" "${psql_arguments[@]}" --command="SELECT count(*) FROM pg_constraint WHERE conname = 'fk_published_revision_same_question' AND convalidated;")" == "1" ]]; then
        fk_published_revision_same_question=true
    fi
    if [[ "$("$POSTGRES_BIN_DIR/psql" "${psql_arguments[@]}" --command="SELECT to_regclass('public.platform_security_events') IS NOT NULL;")" == "t" ]]; then
        platform_security_events=true
    fi
fi

report_path="$MVS01_DR_RESTORE_DIR/restore-report.json"
jq -n \
    --arg format "toi-no-mori-dr-restore-report-v2" \
    --arg backupId "$(jq -r '.backupId' "$manifest")" \
    --arg restoredAtUtc "$(date -u +%Y-%m-%dT%H:%M:%SZ)" \
    --arg dumpSha256 "$actual_sha256" \
    --argjson questionCount "$question_count" \
    --argjson auditCount "$audit_count" \
    --argjson migrationCount "$migration_count" \
    --argjson platformSecurityEventCount "$platform_security_event_count" \
    --arg latestMigrationVersion "$latest_migration_version" \
    --argjson fkPublishedRevisionSameQuestion "$fk_published_revision_same_question" \
    --argjson platformSecurityEvents "$platform_security_events" \
    '{
        format: $format,
        backupId: $backupId,
        restoredAtUtc: $restoredAtUtc,
        dumpSha256: $dumpSha256,
        questionCount: $questionCount,
        auditCount: $auditCount,
        migrationCount: $migrationCount,
        platformSecurityEventCount: $platformSecurityEventCount,
        schemaContract: {
            migrationCount: $migrationCount,
            latestMigrationVersion: $latestMigrationVersion,
            fkPublishedRevisionSameQuestion: $fkPublishedRevisionSameQuestion,
            platformSecurityEvents: $platformSecurityEvents
        }
    }' >"$report_path"
chmod 600 "$report_path"

printf '%s\n' "$report_path"
