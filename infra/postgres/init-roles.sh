#!/usr/bin/env bash
set -euo pipefail

for variable_name in \
    MVS01_POSTGRES_APPLICATION_PASSWORD \
    MVS01_POSTGRES_MIGRATION_PASSWORD \
    MVS01_POSTGRES_PLATFORM_AUDIT_WRITER_PASSWORD \
    MVS01_POSTGRES_PLATFORM_AUDIT_READER_PASSWORD; do
    if [[ -z "${!variable_name:-}" ]]; then
        echo "Required PostgreSQL role secret is missing: $variable_name" >&2
        exit 2
    fi
done

application_role="${MVS01_POSTGRES_APPLICATION_ROLE:-mvs01_app}"
migration_role="${MVS01_POSTGRES_MIGRATION_ROLE:-mvs01_migrator}"
platform_writer_role="${MVS01_POSTGRES_PLATFORM_AUDIT_WRITER_ROLE:-mvs01_platform_audit_writer}"
platform_reader_role="${MVS01_POSTGRES_PLATFORM_AUDIT_READER_ROLE:-mvs01_platform_audit_reader}"
postgres_roles=("$application_role" "$migration_role" "$platform_writer_role" "$platform_reader_role")
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

psql \
    --username="$POSTGRES_USER" \
    --dbname="$POSTGRES_DB" \
    --set=ON_ERROR_STOP=1 \
    --set="application_role=$application_role" \
    --set="application_password=$MVS01_POSTGRES_APPLICATION_PASSWORD" \
    --set="migration_role=$migration_role" \
    --set="migration_password=$MVS01_POSTGRES_MIGRATION_PASSWORD" \
    --set="platform_writer_role=$platform_writer_role" \
    --set="platform_writer_password=$MVS01_POSTGRES_PLATFORM_AUDIT_WRITER_PASSWORD" \
    --set="platform_reader_role=$platform_reader_role" \
    --set="platform_reader_password=$MVS01_POSTGRES_PLATFORM_AUDIT_READER_PASSWORD" \
    --set="database_name=$POSTGRES_DB" \
    <<'SQL'
REVOKE CREATE ON SCHEMA public FROM PUBLIC;

CREATE ROLE :"application_role"
    LOGIN PASSWORD :'application_password'
    NOSUPERUSER NOCREATEDB NOCREATEROLE NOINHERIT NOREPLICATION NOBYPASSRLS;
CREATE ROLE :"migration_role"
    LOGIN PASSWORD :'migration_password'
    NOSUPERUSER NOCREATEDB NOCREATEROLE NOINHERIT NOREPLICATION NOBYPASSRLS;
CREATE ROLE :"platform_writer_role"
    LOGIN PASSWORD :'platform_writer_password'
    NOSUPERUSER NOCREATEDB NOCREATEROLE NOINHERIT NOREPLICATION NOBYPASSRLS;
CREATE ROLE :"platform_reader_role"
    LOGIN PASSWORD :'platform_reader_password'
    NOSUPERUSER NOCREATEDB NOCREATEROLE NOINHERIT NOREPLICATION NOBYPASSRLS;

GRANT CONNECT ON DATABASE :"database_name"
    TO :"application_role", :"migration_role", :"platform_writer_role", :"platform_reader_role";
GRANT USAGE, CREATE ON SCHEMA public TO :"migration_role" WITH GRANT OPTION;
REVOKE CREATE ON SCHEMA public FROM :"application_role";
SQL
