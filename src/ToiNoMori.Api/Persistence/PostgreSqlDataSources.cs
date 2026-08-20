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

public sealed record PostgreSqlPersistenceSettings(string ApplicationRole);
