using System.Reflection;
using Npgsql;

namespace ToiNoMori.Api.Persistence;

public sealed class PostgreSqlMigrator(
    PostgreSqlMigrationDataSource migrationDataSource,
    PostgreSqlPersistenceSettings settings)
{
    private const long MigrationLockId = 8_670_531_901;

    public async Task ApplyAsync(CancellationToken cancellationToken)
    {
        await using var connection = await migrationDataSource.Value.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        await ExecuteAsync(
            connection,
            transaction,
            "SELECT pg_advisory_xact_lock(@lock_id);",
            command => command.Parameters.AddWithValue("lock_id", MigrationLockId),
            cancellationToken);

        await ExecuteAsync(
            connection,
            transaction,
            """
            CREATE TABLE IF NOT EXISTS schema_migrations (
                version varchar(300) PRIMARY KEY,
                applied_at timestamptz NOT NULL
            );
            """,
            null,
            cancellationToken);

        var assembly = typeof(PostgreSqlMigrator).Assembly;
        var resources = assembly.GetManifestResourceNames()
            .Where(name => name.Contains(".Persistence.Migrations.", StringComparison.Ordinal)
                && name.EndsWith(".sql", StringComparison.Ordinal))
            .Order(StringComparer.Ordinal)
            .ToArray();

        foreach (var resourceName in resources)
        {
            if (await WasAppliedAsync(connection, transaction, resourceName, cancellationToken))
            {
                continue;
            }

            var migrationSql = await ReadResourceAsync(assembly, resourceName, cancellationToken);
            await ExecuteAsync(connection, transaction, migrationSql, null, cancellationToken);
            await ExecuteAsync(
                connection,
                transaction,
                "SET CONSTRAINTS ALL IMMEDIATE; SET CONSTRAINTS ALL DEFERRED;",
                null,
                cancellationToken);
            await ExecuteAsync(
                connection,
                transaction,
                "INSERT INTO schema_migrations (version, applied_at) VALUES (@version, @applied_at);",
                command =>
                {
                    command.Parameters.AddWithValue("version", resourceName);
                    command.Parameters.AddWithValue("applied_at", DateTimeOffset.UtcNow);
                },
                cancellationToken);
        }

        var schema = await CurrentSchemaAsync(connection, transaction, cancellationToken);
        await ApplyApplicationGrantsAsync(
            connection,
            transaction,
            schema,
            settings.ApplicationRole,
            settings.PlatformAuditWriterRole,
            settings.PlatformAuditReaderRole,
            cancellationToken);

        await transaction.CommitAsync(cancellationToken);
    }

    private static async Task<string> CurrentSchemaAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand("SELECT current_schema();", connection, transaction);
        return (string?)await command.ExecuteScalarAsync(cancellationToken)
            ?? throw new InvalidOperationException("PostgreSQL current schema could not be resolved.");
    }

    private static Task ApplyApplicationGrantsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string schema,
        string applicationRole,
        string platformAuditWriterRole,
        string platformAuditReaderRole,
        CancellationToken cancellationToken)
    {
        var quotedSchema = QuoteIdentifier(schema);
        var quotedRole = QuoteIdentifier(applicationRole);
        var quotedPlatformWriter = QuoteIdentifier(platformAuditWriterRole);
        var quotedPlatformReader = QuoteIdentifier(platformAuditReaderRole);
        var sql = $"""
            REVOKE CREATE ON SCHEMA {quotedSchema} FROM {quotedRole};
            GRANT USAGE ON SCHEMA {quotedSchema} TO {quotedRole};
            REVOKE CREATE ON SCHEMA {quotedSchema} FROM {quotedPlatformWriter}, {quotedPlatformReader};
            GRANT USAGE ON SCHEMA {quotedSchema} TO {quotedPlatformWriter}, {quotedPlatformReader};

            REVOKE ALL PRIVILEGES ON TABLE {quotedSchema}.schema_migrations
                FROM {quotedRole}, {quotedPlatformWriter}, {quotedPlatformReader};

            REVOKE ALL PRIVILEGES ON TABLE {quotedSchema}.tenants FROM {quotedRole};
            GRANT SELECT ON TABLE {quotedSchema}.tenants TO {quotedRole};

            REVOKE ALL PRIVILEGES ON TABLE
                {quotedSchema}.questions,
                {quotedSchema}.question_revisions,
                {quotedSchema}.idempotency_records,
                {quotedSchema}.audit_events
            FROM {quotedRole};
            GRANT SELECT, INSERT, UPDATE
                ON TABLE {quotedSchema}.questions TO {quotedRole};
            GRANT SELECT, INSERT
                ON TABLE {quotedSchema}.question_revisions TO {quotedRole};
            GRANT SELECT, INSERT, DELETE
                ON TABLE {quotedSchema}.idempotency_records TO {quotedRole};
            GRANT SELECT, INSERT
                ON TABLE {quotedSchema}.audit_events TO {quotedRole};

            REVOKE ALL PRIVILEGES ON TABLE {quotedSchema}.platform_security_events
                FROM {quotedRole}, {quotedPlatformWriter}, {quotedPlatformReader};
            GRANT INSERT ON TABLE {quotedSchema}.platform_security_events
                TO {quotedPlatformWriter};
            GRANT SELECT ON TABLE {quotedSchema}.platform_security_events
                TO {quotedPlatformReader};

            GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA {quotedSchema} TO {quotedRole};
            """;
        return ExecuteAsync(connection, transaction, sql, null, cancellationToken);
    }

    private static string QuoteIdentifier(string value) =>
        $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";

    private static async Task<bool> WasAppliedAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string version,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            "SELECT EXISTS (SELECT 1 FROM schema_migrations WHERE version = @version);",
            connection,
            transaction);
        command.Parameters.AddWithValue("version", version);
        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result is true;
    }

    private static async Task<string> ReadResourceAsync(
        Assembly assembly,
        string resourceName,
        CancellationToken cancellationToken)
    {
        await using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded migration was not found: {resourceName}");
        using var reader = new StreamReader(stream);
        return await reader.ReadToEndAsync(cancellationToken);
    }

    private static async Task ExecuteAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string sql,
        Action<NpgsqlCommand>? addParameters,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        addParameters?.Invoke(command);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
