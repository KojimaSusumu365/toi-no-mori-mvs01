#!/usr/bin/env bash
set -euo pipefail

for variable_name in \
    MVS01_POSTGRES_APPLICATION_PASSWORD \
    MVS01_POSTGRES_MIGRATION_PASSWORD; do
    if [[ -z "${!variable_name:-}" ]]; then
        echo "Required PostgreSQL role secret is missing: $variable_name" >&2
        exit 2
    fi
done

application_role="${MVS01_POSTGRES_APPLICATION_ROLE:-mvs01_app}"
migration_role="${MVS01_POSTGRES_MIGRATION_ROLE:-mvs01_migrator}"
if [[ ! "$application_role" =~ ^[a-z_][a-z0-9_]{0,62}$ \
    || ! "$migration_role" =~ ^[a-z_][a-z0-9_]{0,62}$ \
    || "$application_role" == "$migration_role" ]]; then
    echo "PostgreSQL application/migration role名が安全な分離条件を満たしません。" >&2
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
    --set="database_name=$POSTGRES_DB" \
    <<'SQL'
REVOKE CREATE ON SCHEMA public FROM PUBLIC;

CREATE ROLE :"application_role"
    LOGIN PASSWORD :'application_password'
    NOSUPERUSER NOCREATEDB NOCREATEROLE NOINHERIT NOREPLICATION NOBYPASSRLS;
CREATE ROLE :"migration_role"
    LOGIN PASSWORD :'migration_password'
    NOSUPERUSER NOCREATEDB NOCREATEROLE NOINHERIT NOREPLICATION NOBYPASSRLS;

GRANT CONNECT ON DATABASE :"database_name" TO :"application_role", :"migration_role";
GRANT USAGE, CREATE ON SCHEMA public TO :"migration_role" WITH GRANT OPTION;
REVOKE CREATE ON SCHEMA public FROM :"application_role";
SQL
