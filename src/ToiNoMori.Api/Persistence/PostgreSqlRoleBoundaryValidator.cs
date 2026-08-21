using Npgsql;

namespace ToiNoMori.Api.Persistence;

public sealed class PostgreSqlRoleBoundaryValidator(
    PostgreSqlApplicationDataSource applicationDataSource)
{
    public async Task ValidateAsync(CancellationToken cancellationToken)
    {
        await using var connection = await applicationDataSource.Value.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(
            """
            WITH application_role AS (
                SELECT oid, rolsuper, rolinherit, rolbypassrls
                FROM pg_roles
                WHERE rolname = current_user
            ),
            protected_tables AS (
                SELECT table_class.oid, table_class.relname, table_class.relowner
                FROM pg_class AS table_class
                JOIN pg_namespace AS table_namespace
                  ON table_namespace.oid = table_class.relnamespace
                WHERE table_namespace.nspname = current_schema()
                  AND table_class.relname = ANY (
                      ARRAY['questions', 'question_revisions', 'idempotency_records', 'audit_events'])
            ),
            tenant_table AS (
                SELECT table_class.oid
                FROM pg_class AS table_class
                JOIN pg_namespace AS table_namespace
                  ON table_namespace.oid = table_class.relnamespace
                WHERE table_namespace.nspname = current_schema()
                  AND table_class.relname = 'tenants'
            ),
            migration_table AS (
                SELECT table_class.oid
                FROM pg_class AS table_class
                JOIN pg_namespace AS table_namespace
                  ON table_namespace.oid = table_class.relnamespace
                WHERE table_namespace.nspname = current_schema()
                  AND table_class.relname = 'schema_migrations'
            ),
            platform_audit_table AS (
                SELECT table_class.oid
                FROM pg_class AS table_class
                JOIN pg_namespace AS table_namespace
                  ON table_namespace.oid = table_class.relnamespace
                WHERE table_namespace.nspname = current_schema()
                  AND table_class.relname = 'platform_security_events'
            )
            SELECT
                NOT role.rolsuper
                AND NOT role.rolinherit
                AND NOT role.rolbypassrls
                AND NOT has_schema_privilege(role.oid, current_schema(), 'CREATE')
                AND has_schema_privilege(role.oid, current_schema(), 'USAGE')
                AND (SELECT count(*) = 4 FROM protected_tables)
                AND NOT EXISTS (
                    SELECT 1 FROM protected_tables WHERE relowner = role.oid)
                AND EXISTS (
                    SELECT 1 FROM protected_tables
                    WHERE relname = 'questions'
                      AND has_table_privilege(role.oid, oid, 'SELECT')
                      AND has_table_privilege(role.oid, oid, 'INSERT')
                      AND has_table_privilege(role.oid, oid, 'UPDATE')
                      AND NOT has_table_privilege(
                          role.oid, oid, 'DELETE,TRUNCATE,REFERENCES,TRIGGER'))
                AND EXISTS (
                    SELECT 1 FROM protected_tables
                    WHERE relname = 'question_revisions'
                      AND has_table_privilege(role.oid, oid, 'SELECT')
                      AND has_table_privilege(role.oid, oid, 'INSERT')
                      AND NOT has_table_privilege(
                          role.oid, oid, 'UPDATE,DELETE,TRUNCATE,REFERENCES,TRIGGER'))
                AND EXISTS (
                    SELECT 1 FROM protected_tables
                    WHERE relname = 'idempotency_records'
                      AND has_table_privilege(role.oid, oid, 'SELECT')
                      AND has_table_privilege(role.oid, oid, 'INSERT')
                      AND has_table_privilege(role.oid, oid, 'DELETE')
                      AND NOT has_table_privilege(
                          role.oid, oid, 'UPDATE,TRUNCATE,REFERENCES,TRIGGER'))
                AND EXISTS (
                    SELECT 1 FROM protected_tables
                    WHERE relname = 'audit_events'
                      AND has_table_privilege(role.oid, oid, 'SELECT')
                      AND has_table_privilege(role.oid, oid, 'INSERT')
                      AND NOT has_table_privilege(
                          role.oid, oid, 'UPDATE,DELETE,TRUNCATE,REFERENCES,TRIGGER'))
                AND (SELECT count(*) = 1 FROM tenant_table)
                AND NOT EXISTS (
                    SELECT 1
                    FROM tenant_table
                    WHERE NOT has_table_privilege(role.oid, oid, 'SELECT')
                       OR has_table_privilege(
                           role.oid, oid, 'INSERT,UPDATE,DELETE,TRUNCATE,REFERENCES,TRIGGER'))
                AND (SELECT count(*) = 1 FROM migration_table)
                AND NOT EXISTS (
                    SELECT 1
                    FROM migration_table
                    WHERE has_table_privilege(
                        role.oid, oid, 'SELECT,INSERT,UPDATE,DELETE,TRUNCATE,REFERENCES,TRIGGER'))
                AND (SELECT count(*) = 1 FROM platform_audit_table)
                AND NOT EXISTS (
                    SELECT 1
                    FROM platform_audit_table
                    WHERE has_table_privilege(
                        role.oid, oid, 'SELECT,INSERT,UPDATE,DELETE,TRUNCATE,REFERENCES,TRIGGER'))
            FROM application_role AS role;
            """,
            connection);
        var valid = (bool)(await command.ExecuteScalarAsync(cancellationToken) ?? false);
        if (!valid)
        {
            throw new InvalidOperationException(
                "PostgreSQL application role violates the required least-privilege boundary.");
        }
    }
}
