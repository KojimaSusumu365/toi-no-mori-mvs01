#!/usr/bin/env bash
set -euo pipefail

require_variable() {
    local name="$1"
    if [[ -z "${!name:-}" ]]; then
        echo "Required environment variable is missing: $name" >&2
        exit 2
    fi
}

for name in \
    MVS01_DR_BACKUP_FILE \
    MVS01_DR_METADATA_FILE \
    MVS01_DR_SOURCE_BUCKET \
    MVS01_DR_DESTINATION_BUCKET \
    MVS01_DR_OBJECT_PREFIX; do
    require_variable "$name"
done

AWS_CLI="${AWS_CLI:-aws}"
SOURCE_ENDPOINT="${MVS01_DR_SOURCE_ENDPOINT:-https://s3.isk01.sakurastorage.jp}"
DESTINATION_ENDPOINT="${MVS01_DR_DESTINATION_ENDPOINT:-https://s3.tky01.sakurastorage.jp}"
SOURCE_REGION="${MVS01_DR_SOURCE_REGION:-jp-north-1}"
DESTINATION_REGION="${MVS01_DR_DESTINATION_REGION:-jp-east-1}"
MAX_WAIT_SECONDS="${MVS01_DR_CRR_MAX_WAIT_SECONDS:-1800}"

if ! command -v "$AWS_CLI" >/dev/null 2>&1; then
    echo "AWS CLI was not found. Install a version supported by Sakura Object Storage." >&2
    exit 2
fi
for executable in jq stat; do
    if ! command -v "$executable" >/dev/null 2>&1; then
        echo "Required executable was not found: $executable" >&2
        exit 2
    fi
done
for readable_file in "$MVS01_DR_BACKUP_FILE" "$MVS01_DR_METADATA_FILE"; do
    if [[ ! -r "$readable_file" ]]; then
        echo "Required upload input is not readable: $readable_file" >&2
        exit 2
    fi
done

backup_name="$(basename "$MVS01_DR_BACKUP_FILE")"
metadata_name="$(basename "$MVS01_DR_METADATA_FILE")"
backup_key="${MVS01_DR_OBJECT_PREFIX%/}/$backup_name"
metadata_key="${MVS01_DR_OBJECT_PREFIX%/}/$metadata_name"
ciphertext_sha256="$(jq -r '.ciphertextSha256' "$MVS01_DR_METADATA_FILE")"
ciphertext_size="$(stat -c '%s' "$MVS01_DR_BACKUP_FILE")"

"$AWS_CLI" \
    --endpoint-url "$SOURCE_ENDPOINT" \
    --region "$SOURCE_REGION" \
    s3api put-object \
    --bucket "$MVS01_DR_SOURCE_BUCKET" \
    --key "$backup_key" \
    --body "$MVS01_DR_BACKUP_FILE" \
    --content-type application/pkcs7-mime \
    --metadata "sha256=$ciphertext_sha256" >/dev/null

"$AWS_CLI" \
    --endpoint-url "$SOURCE_ENDPOINT" \
    --region "$SOURCE_REGION" \
    s3api put-object \
    --bucket "$MVS01_DR_SOURCE_BUCKET" \
    --key "$metadata_key" \
    --body "$MVS01_DR_METADATA_FILE" \
    --content-type application/json >/dev/null

deadline=$((SECONDS + MAX_WAIT_SECONDS))
while (( SECONDS < deadline )); do
    source_head="$($AWS_CLI \
        --endpoint-url "$SOURCE_ENDPOINT" \
        --region "$SOURCE_REGION" \
        s3api head-object \
        --bucket "$MVS01_DR_SOURCE_BUCKET" \
        --key "$backup_key")"
    replication_status="$(jq -r '.ReplicationStatus // empty' <<<"$source_head")"
    if [[ "$replication_status" == "COMPLETED" ]]; then
        if destination_head="$($AWS_CLI \
            --endpoint-url "$DESTINATION_ENDPOINT" \
            --region "$DESTINATION_REGION" \
            s3api head-object \
            --bucket "$MVS01_DR_DESTINATION_BUCKET" \
            --key "$backup_key" 2>/dev/null)"; then
            destination_size="$(jq -r '.ContentLength' <<<"$destination_head")"
            destination_sha256="$(jq -r '.Metadata.sha256 // empty' <<<"$destination_head")"
            if [[ "$destination_size" == "$ciphertext_size" \
                && "$destination_sha256" == "$ciphertext_sha256" ]]; then
                printf '%s\n' "$backup_key"
                exit 0
            fi
        fi
    fi
    sleep 10
done

echo "CRR did not reach a verified completed state before timeout." >&2
exit 5
