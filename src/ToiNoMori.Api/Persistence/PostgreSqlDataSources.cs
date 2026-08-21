using Npgsql;

namespace ToiNoMori.Api.Persistence;

public sealed class PostgreSqlApplicationDataSource(NpgsqlDataSource value) : IAsyncDisposable
{
    public NpgsqlDataSource Value { get; } = value;

    public ValueTask DisposeAsync() => Value.DisposeAsync();
}

public sealed class PostgreSqlMigrationDataSource(NpgsqlDataSource value) : IAsyncDisposable
{
    public NpgsqlDataSource Value { get; } = value;

    public ValueTask DisposeAsync() => Value.DisposeAsync();
}

public sealed class PostgreSqlPlatformAuditWriterDataSource(NpgsqlDataSource value) : IAsyncDisposable
{
    public NpgsqlDataSource Value { get; } = value;

    public ValueTask DisposeAsync() => Value.DisposeAsync();
}

public sealed class PostgreSqlPlatformAuditReaderDataSource(NpgsqlDataSource value) : IAsyncDisposable
{
    public NpgsqlDataSource Value { get; } = value;

    public ValueTask DisposeAsync() => Value.DisposeAsync();
}

public sealed record PostgreSqlPersistenceSettings(
    string ApplicationRole,
    string PlatformAuditWriterRole,
    string PlatformAuditReaderRole);
