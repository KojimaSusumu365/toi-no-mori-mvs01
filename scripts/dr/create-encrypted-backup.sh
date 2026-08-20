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
    MVS01_SOURCE_PGHOST \
    MVS01_SOURCE_PGPORT \
    MVS01_SOURCE_PGUSER \
    MVS01_SOURCE_PGDATABASE \
    MVS01_DR_OUTPUT_DIR \
    MVS01_DR_SIGNER_CERT \
    MVS01_DR_SIGNER_KEY \
    MVS01_DR_RECIPIENT_CERT; do
    require_variable "$name"
done

POSTGRES_BIN_DIR="${POSTGRES_BIN_DIR:-/usr/bin}"
OPENSSL_BIN="${OPENSSL_BIN:-openssl}"
SOURCE_SITE="${MVS01_DR_SOURCE_SITE:-ishikari-primary}"

for executable in \
    "$POSTGRES_BIN_DIR/pg_dump" \
    "$OPENSSL_BIN" \
    jq \
    sha256sum \
    tar \
    flock; do
    if ! command -v "$executable" >/dev/null 2>&1; then
        echo "Required executable was not found: $executable" >&2
        exit 2
    fi
done

for readable_file in \
    "$MVS01_DR_SIGNER_CERT" \
    "$MVS01_DR_SIGNER_KEY" \
    "$MVS01_DR_RECIPIENT_CERT"; do
    if [[ ! -r "$readable_file" ]]; then
        echo "Required key material is not readable: $readable_file" >&2
        exit 2
    fi
done

mkdir -p -- "$MVS01_DR_OUTPUT_DIR"
chmod 700 "$MVS01_DR_OUTPUT_DIR"

work_directory="$(mktemp -d /tmp/toi-no-mori-backup.XXXXXX)"
cleanup() {
    case "$work_directory" in
        /tmp/toi-no-mori-backup.*) rm -rf -- "$work_directory" ;;
    esac
}
trap cleanup EXIT

exec 9>"$MVS01_DR_OUTPUT_DIR/.backup.lock"
if ! flock -n 9; then
    echo "Another backup process already holds the output lock." >&2
    exit 3
fi

if [[ -n "${MVS01_SOURCE_PGPASSFILE:-}" ]]; then
    if [[ ! -r "$MVS01_SOURCE_PGPASSFILE" ]]; then
        echo "MVS01_SOURCE_PGPASSFILE is not readable." >&2
        exit 2
    fi
    export PGPASSFILE="$MVS01_SOURCE_PGPASSFILE"
fi

snapshot_started_at="$(date -u +%Y-%m-%dT%H:%M:%SZ)"
backup_id="$(date -u +%Y%m%dT%H%M%SZ)-$($OPENSSL_BIN rand -hex 8)"
dump_file="$work_directory/database.dump"

"$POSTGRES_BIN_DIR/pg_dump" \
    --host="$MVS01_SOURCE_PGHOST" \
    --port="$MVS01_SOURCE_PGPORT" \
    --username="$MVS01_SOURCE_PGUSER" \
    --dbname="$MVS01_SOURCE_PGDATABASE" \
    --format=custom \
    --compress=none \
    --no-owner \
    --no-privileges \
    --file="$dump_file"

dump_sha256="$(sha256sum "$dump_file" | awk '{print $1}')"
snapshot_completed_at="$(date -u +%Y-%m-%dT%H:%M:%SZ)"
postgres_tool_version="$("$POSTGRES_BIN_DIR/pg_dump" --version)"

jq -n \
    --arg format "toi-no-mori-dr-backup-v1" \
    --arg backupId "$backup_id" \
    --arg sourceSite "$SOURCE_SITE" \
    --arg snapshotStartedAtUtc "$snapshot_started_at" \
    --arg snapshotCompletedAtUtc "$snapshot_completed_at" \
    --arg dumpSha256 "$dump_sha256" \
    --arg postgresToolVersion "$postgres_tool_version" \
    '{
        format: $format,
        backupId: $backupId,
        sourceSite: $sourceSite,
        snapshotStartedAtUtc: $snapshotStartedAtUtc,
        snapshotCompletedAtUtc: $snapshotCompletedAtUtc,
        dumpSha256: $dumpSha256,
        postgresToolVersion: $postgresToolVersion
    }' >"$work_directory/manifest.json"

tar --format=ustar \
    -C "$work_directory" \
    -cf "$work_directory/payload.tar" \
    manifest.json \
    database.dump

sign_arguments=(
    cms -sign -binary -stream -nodetach -cades
    -md sha256
    -in "$work_directory/payload.tar"
    -signer "$MVS01_DR_SIGNER_CERT"
    -inkey "$MVS01_DR_SIGNER_KEY"
    -outform DER
    -out "$work_directory/payload.signed.cms"
)
if [[ -n "${MVS01_DR_SIGNER_KEY_PASS_FILE:-}" ]]; then
    sign_arguments+=( -passin "file:$MVS01_DR_SIGNER_KEY_PASS_FILE" )
fi
"$OPENSSL_BIN" "${sign_arguments[@]}"

partial_path="$MVS01_DR_OUTPUT_DIR/$backup_id.p7m.partial"
final_path="$MVS01_DR_OUTPUT_DIR/$backup_id.p7m"
"$OPENSSL_BIN" cms -encrypt \
    -binary \
    -stream \
    -aes-256-gcm \
    -in "$work_directory/payload.signed.cms" \
    -outform DER \
    -out "$partial_path" \
    "$MVS01_DR_RECIPIENT_CERT"
chmod 600 "$partial_path"
mv -- "$partial_path" "$final_path"

ciphertext_sha256="$(sha256sum "$final_path" | awk '{print $1}')"
ciphertext_size="$(stat -c '%s' "$final_path")"
metadata_path="$MVS01_DR_OUTPUT_DIR/$backup_id.metadata.json"
jq -n \
    --arg format "toi-no-mori-dr-object-v1" \
    --arg backupId "$backup_id" \
    --arg sourceSite "$SOURCE_SITE" \
    --arg createdAtUtc "$snapshot_completed_at" \
    --arg ciphertextSha256 "$ciphertext_sha256" \
    --arg fileName "$(basename "$final_path")" \
    --argjson ciphertextSizeBytes "$ciphertext_size" \
    '{
        format: $format,
        backupId: $backupId,
        sourceSite: $sourceSite,
        createdAtUtc: $createdAtUtc,
        ciphertextSha256: $ciphertextSha256,
        ciphertextSizeBytes: $ciphertextSizeBytes,
        fileName: $fileName
    }' >"$metadata_path"
chmod 600 "$metadata_path"

printf '%s\n' "$final_path"
