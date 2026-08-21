using Npgsql;
using NpgsqlTypes;

namespace ToiNoMori.Api.Persistence;

public sealed class PostgreSqlPlatformSecurityAuditSink(
    PostgreSqlPlatformAuditWriterDataSource writerDataSource) : IAuditSink
{
    public async Task<AuditOutcomeRecorded> WriteAsync(
        AccessDenialAuditEnvelope envelope,
        CancellationToken cancellationToken)
    {
        await using var connection = await writerDataSource.Value.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(
            """
            INSERT INTO platform_security_events (
                id, occurred_at, reason_code, normalized_action, partition_hash,
                request_id, correlation_id, occurrence_count, window_started_at)
            VALUES (
                @id, @occurred_at, @reason_code, @normalized_action, @partition_hash,
                @request_id, @correlation_id, 1, @window_started_at)
            ON CONFLICT (partition_hash, normalized_action, window_started_at)
                WHERE reason_code = 'access.rate_limited'
            DO NOTHING;
            """,
            connection);
        command.Parameters.AddWithValue("id", envelope.Id);
        command.Parameters.AddWithValue("occurred_at", envelope.OccurredAt);
        command.Parameters.AddWithValue("reason_code", envelope.ReasonCode);
        command.Parameters.AddWithValue("normalized_action", envelope.NormalizedAction);
        command.Parameters.AddWithValue("partition_hash", envelope.PartitionHash);
        command.Parameters.AddWithValue("request_id", envelope.RequestId);
        command.Parameters.AddWithValue("correlation_id", envelope.CorrelationId);
        command.Parameters.AddWithValue(
            "window_started_at",
            NpgsqlDbType.TimestampTz,
            envelope.WindowStartedAt is null ? DBNull.Value : envelope.WindowStartedAt.Value);
        var written = await command.ExecuteNonQueryAsync(cancellationToken);
        return written == 1
            ? AuditOutcomeRecorded.Written
            : AuditOutcomeRecorded.DuplicateSuppressed;
    }
}

public sealed class PostgreSqlPlatformSecurityEventReader(
    PostgreSqlPlatformAuditReaderDataSource readerDataSource) : IPlatformSecurityEventReader
{
    public async Task<IReadOnlyList<PlatformSecurityEvent>> ReadAsync(
        DateTimeOffset fromInclusive,
        DateTimeOffset toExclusive,
        int limit,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var connection = await readerDataSource.Value.OpenConnectionAsync(cancellationToken);
            await using var command = new NpgsqlCommand(
                """
                SELECT id, occurred_at, reason_code, normalized_action, partition_hash,
                       request_id, correlation_id, occurrence_count, window_started_at
                FROM platform_security_events
                WHERE occurred_at >= @from_inclusive
                  AND occurred_at < @to_exclusive
                ORDER BY occurred_at DESC, id DESC
                LIMIT @limit;
                """,
                connection);
            command.Parameters.AddWithValue("from_inclusive", fromInclusive);
            command.Parameters.AddWithValue("to_exclusive", toExclusive);
            command.Parameters.AddWithValue("limit", limit);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            var events = new List<PlatformSecurityEvent>();
            while (await reader.ReadAsync(cancellationToken))
            {
                events.Add(new(
                    reader.GetGuid(0),
                    new DateTimeOffset(reader.GetDateTime(1).ToUniversalTime()),
                    reader.GetString(2),
                    reader.GetString(3),
                    reader.GetString(4),
                    reader.GetString(5),
                    reader.GetString(6),
                    reader.GetInt32(7),
                    reader.IsDBNull(8)
                        ? null
                        : new DateTimeOffset(reader.GetDateTime(8).ToUniversalTime())));
            }

            return events;
        }
        catch (Exception exception) when (exception is NpgsqlException or TimeoutException)
        {
            throw new StoreUnavailableException(exception);
        }
    }
}
