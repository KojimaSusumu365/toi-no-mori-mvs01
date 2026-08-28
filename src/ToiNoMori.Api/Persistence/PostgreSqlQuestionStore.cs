using System.Text.Json;
using Npgsql;
using NpgsqlTypes;
using ToiNoMori.Domain;

namespace ToiNoMori.Api.Persistence;

public sealed class PostgreSqlQuestionStore(
    PostgreSqlApplicationDataSource applicationDataSource,
    PostgreSqlMigrator migrator,
    PostgreSqlRoleBoundaryValidator roleBoundaryValidator,
    PublicReadTenantContext publicReadTenant,
    TimeProvider timeProvider,
    ILogger<PostgreSqlQuestionStore> logger) : IQuestionStore
{
    private static readonly JsonSerializerOptions SnapshotJsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly Action<ILogger, string, string, string, string, Exception?> DatabaseFailureLog =
        LoggerMessage.Define<string, string, string, string>(
            LogLevel.Error,
            new EventId(6001, "PostgreSqlPersistenceFailure"),
            "PostgreSQL persistence failure. Category={Category}; SqlState={SqlState}; Table={Table}; Constraint={Constraint}.");
    private readonly NpgsqlDataSource dataSource = applicationDataSource.Value;

    public Task InitializeAsync(CancellationToken cancellationToken) => TranslateAvailabilityAsync(async () =>
    {
        await migrator.ApplyAsync(cancellationToken);
        await roleBoundaryValidator.ValidateAsync(cancellationToken);
    });

    public async Task<bool> IsReadyAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
            await using var command = new NpgsqlCommand("SELECT 1;", connection);
            await command.ExecuteScalarAsync(cancellationToken);
            return true;
        }
        catch (Exception exception) when (IsAvailabilityFailure(exception))
        {
            return false;
        }
    }

    public Task<QuestionSnapshot> CreateAsync(
        Guid tenantId,
        ValidatedQuestionContent content,
        string actor,
        string correlationId,
        CancellationToken cancellationToken) => TranslateAvailabilityAsync(async () =>
    {
        var now = timeProvider.GetUtcNow();
        var question = new Question(
            Guid.NewGuid(),
            tenantId,
            content.Title,
            content.Body,
            content.Tags,
            actor,
            now);
        var snapshot = question.Snapshot();

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await SetTenantAsync(connection, transaction, tenantId, cancellationToken);
        await InsertQuestionAsync(connection, transaction, snapshot, cancellationToken);
        await InsertRevisionAsync(connection, transaction, snapshot, cancellationToken);
        await InsertAuditAsync(
            connection,
            transaction,
            tenantId,
            actor,
            snapshot.Id,
            "question.create",
            "success",
            correlationId,
            now,
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return snapshot;
    });

    public Task<QuestionSnapshot> UpdateAsync(
        Guid tenantId,
        Guid id,
        ValidatedQuestionContent content,
        int expectedVersion,
        string actor,
        string correlationId,
        CancellationToken cancellationToken) => MutateAsync(
            tenantId,
            id,
            actor,
            "question.update",
            correlationId,
            (question, now) =>
            {
                question.Update(content.Title, content.Body, content.Tags, expectedVersion, actor, now);
                return question.Snapshot();
            },
            cancellationToken);

    public Task<QuestionSnapshot> SubmitAsync(
        Guid tenantId,
        Guid id,
        string actor,
        string correlationId,
        CancellationToken cancellationToken) => MutateAsync(
            tenantId,
            id,
            actor,
            "question.submit",
            correlationId,
            (question, now) =>
            {
                question.Submit(actor, now);
                return question.Snapshot();
            },
            cancellationToken);

    public Task<QuestionSnapshot> ReturnForChangesAsync(
        Guid tenantId,
        Guid id,
        string reviewer,
        string reason,
        string correlationId,
        CancellationToken cancellationToken) => MutateAsync(
            tenantId,
            id,
            reviewer,
            "question.return",
            correlationId,
            (question, now) =>
            {
                question.ReturnForChanges(reviewer, reason, now);
                return question.Snapshot();
            },
            cancellationToken);

    public Task<QuestionSnapshot> ApproveAsync(
        Guid tenantId,
        Guid id,
        string reviewer,
        int expectedVersion,
        string idempotencyKey,
        string correlationId,
        CancellationToken cancellationToken) => TranslateAvailabilityAsync(async () =>
    {
        var fingerprint = $"question.approve:{tenantId}:{id}:{reviewer}:{expectedVersion}";
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await SetTenantAsync(connection, transaction, tenantId, cancellationToken);
        var now = timeProvider.GetUtcNow();

        await using (var lockCommand = new NpgsqlCommand(
            "SELECT pg_advisory_xact_lock(hashtextextended(@key, 0));",
            connection,
            transaction))
        {
            lockCommand.Parameters.AddWithValue("key", $"{tenantId}:{idempotencyKey}");
            await lockCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        await DeleteExpiredIdempotencyAsync(
            connection,
            transaction,
            tenantId,
            idempotencyKey,
            now,
            cancellationToken);

        var existing = await ReadIdempotencyAsync(
            connection,
            transaction,
            tenantId,
            idempotencyKey,
            cancellationToken);
        if (existing is not null)
        {
            if (!string.Equals(existing.Value.Fingerprint, fingerprint, StringComparison.Ordinal))
            {
                throw new DomainRuleViolationException(
                    "idempotency.key_reused",
                    "The idempotency key was already used for a different command.");
            }

            await transaction.CommitAsync(cancellationToken);
            return existing.Value.Result;
        }

        try
        {
            var question = await LoadRequiredAsync(connection, transaction, tenantId, id, cancellationToken);
            question.Approve(reviewer, expectedVersion, now);
            var result = question.Snapshot();
            await UpdateQuestionAsync(connection, transaction, result, cancellationToken);
            await InsertRevisionAsync(connection, transaction, result, cancellationToken);
            await InsertAuditAsync(
                connection,
                transaction,
                tenantId,
                reviewer,
                id,
                "question.approve",
                "success",
                correlationId,
                now,
                cancellationToken);
            await InsertIdempotencyAsync(
                connection,
                transaction,
                tenantId,
                idempotencyKey,
                reviewer,
                expectedVersion,
                fingerprint,
                result,
                now,
                cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return result;
        }
        catch (DomainRuleViolationException exception)
        {
            await InsertAuditAsync(
                connection,
                transaction,
                tenantId,
                reviewer,
                id,
                "question.approve",
                $"rejected:{exception.Code}",
                correlationId,
                now,
                cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            throw;
        }
    });

    public Task<QuestionSnapshot> WithdrawAsync(
        Guid tenantId,
        Guid id,
        string actor,
        string reason,
        string correlationId,
        CancellationToken cancellationToken) => MutateAsync(
            tenantId,
            id,
            actor,
            "question.withdraw",
            correlationId,
            (question, now) =>
            {
                question.Withdraw(actor, reason, now);
                return question.Snapshot();
            },
            cancellationToken);

    public Task<QuestionSnapshot?> FindAdministrativeAsync(
        Guid tenantId,
        Guid id,
        string actor,
        bool isReviewer,
        CancellationToken cancellationToken) => TranslateAvailabilityAsync(async () =>
        {
            await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
            await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
            await SetTenantAsync(connection, transaction, tenantId, cancellationToken);
            await using var command = new NpgsqlCommand(
                $"{SelectColumns} WHERE tenant_id = @tenant_id AND id = @id AND (@is_reviewer OR owner_subject = @actor);",
                connection,
                transaction);
            command.Parameters.AddWithValue("tenant_id", tenantId);
            command.Parameters.AddWithValue("id", id);
            command.Parameters.AddWithValue("is_reviewer", isReviewer);
            command.Parameters.AddWithValue("actor", actor);
            QuestionSnapshot? result;
            await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
            {
                result = await reader.ReadAsync(cancellationToken) ? ReadSnapshot(reader) : null;
            }
            await transaction.CommitAsync(cancellationToken);
            return result;
        });

    public Task<IReadOnlyList<QuestionSnapshot>> SearchAdministrativeAsync(
        Guid tenantId,
        string actor,
        bool isReviewer,
        QuestionStatus? status,
        int limit,
        CancellationToken cancellationToken) => TranslateAvailabilityAsync<IReadOnlyList<QuestionSnapshot>>(async () =>
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await SetTenantAsync(connection, transaction, tenantId, cancellationToken);
        await using var command = new NpgsqlCommand(
            $"""
            {SelectColumns}
            WHERE tenant_id = @tenant_id
              AND (@is_reviewer OR owner_subject = @actor)
              AND (@status = '' OR status = @status)
            ORDER BY updated_at DESC, id
            LIMIT @limit;
            """,
            connection,
            transaction);
        command.Parameters.AddWithValue("tenant_id", tenantId);
        command.Parameters.AddWithValue("is_reviewer", isReviewer);
        command.Parameters.AddWithValue("actor", actor);
        command.Parameters.AddWithValue("status", status is null ? string.Empty : ToDatabaseStatus(status.Value));
        command.Parameters.AddWithValue("limit", Math.Clamp(limit, 1, 100));

        var results = new List<QuestionSnapshot>();
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                results.Add(ReadSnapshot(reader));
            }
        }

        await transaction.CommitAsync(cancellationToken);
        return results;
    });

    public Task<QuestionSnapshot?> FindPublicAsync(Guid id, CancellationToken cancellationToken) =>
        TranslateAvailabilityAsync(async () =>
        {
            await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
            await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
            await SetTenantAsync(connection, transaction, publicReadTenant.TenantId, cancellationToken);
            await using var command = new NpgsqlCommand(
                $"{SelectColumns} WHERE tenant_id = @tenant_id AND id = @id AND status = 'PUBLISHED';",
                connection,
                transaction);
            command.Parameters.AddWithValue("tenant_id", publicReadTenant.TenantId);
            command.Parameters.AddWithValue("id", id);
            QuestionSnapshot? result;
            await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
            {
                result = await reader.ReadAsync(cancellationToken) ? ReadSnapshot(reader) : null;
            }
            await transaction.CommitAsync(cancellationToken);
            return result;
        });

    public Task<IReadOnlyList<QuestionSnapshot>> SearchPublicAsync(
        string? query,
        string? tag,
        int limit,
        CancellationToken cancellationToken) => TranslateAvailabilityAsync<IReadOnlyList<QuestionSnapshot>>(async () =>
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await SetTenantAsync(connection, transaction, publicReadTenant.TenantId, cancellationToken);
        await using var command = new NpgsqlCommand(
            $"""
            {SelectColumns}
            WHERE tenant_id = @tenant_id
              AND status = 'PUBLISHED'
              AND (@query = '' OR title ILIKE @pattern ESCAPE '\\'
                   OR body ILIKE @pattern ESCAPE '\\'
                   OR EXISTS (SELECT 1 FROM unnest(tags) AS value WHERE value ILIKE @pattern ESCAPE '\\'))
              AND (@tag = '' OR EXISTS (SELECT 1 FROM unnest(tags) AS value WHERE lower(value) = lower(@tag)))
            ORDER BY published_at DESC, id
            LIMIT @limit;
            """,
            connection,
            transaction);
        var normalizedQuery = query?.Trim() ?? string.Empty;
        command.Parameters.AddWithValue("tenant_id", publicReadTenant.TenantId);
        command.Parameters.AddWithValue("query", normalizedQuery);
        command.Parameters.AddWithValue("pattern", $"%{EscapeLike(normalizedQuery)}%");
        command.Parameters.AddWithValue("tag", tag?.Trim() ?? string.Empty);
        command.Parameters.AddWithValue("limit", Math.Clamp(limit, 1, 50));

        var results = new List<QuestionSnapshot>();
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                results.Add(ReadSnapshot(reader));
            }
        }

        await transaction.CommitAsync(cancellationToken);
        return results;
    });

    public Task<IReadOnlyList<AuditRecord>> ReadAuditAsync(
        Guid tenantId,
        Guid? targetId,
        int limit,
        CancellationToken cancellationToken) =>
        TranslateAvailabilityAsync<IReadOnlyList<AuditRecord>>(async () =>
        {
            await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
            await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
            await SetTenantAsync(connection, transaction, tenantId, cancellationToken);
            await using var command = new NpgsqlCommand(
                """
                SELECT id, tenant_id, actor_subject, target_id, action, result, correlation_id, occurred_at
                FROM audit_events
                WHERE tenant_id = @tenant_id
                  AND (@target_id IS NULL OR target_id = @target_id)
                ORDER BY occurred_at DESC, sequence_id DESC
                LIMIT @limit;
                """,
                connection,
                transaction);
            command.Parameters.Add("tenant_id", NpgsqlDbType.Uuid).Value = tenantId;
            command.Parameters.Add("target_id", NpgsqlDbType.Uuid).Value =
                targetId is { } value ? value : DBNull.Value;
            command.Parameters.Add("limit", NpgsqlDbType.Integer).Value = Math.Clamp(limit, 1, 200);
            var results = new List<AuditRecord>();
            await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
            {
                while (await reader.ReadAsync(cancellationToken))
                {
                    results.Add(new(
                        reader.GetGuid(0),
                        reader.GetGuid(1),
                        reader.GetString(2),
                        reader.GetGuid(3),
                        reader.GetString(4),
                        reader.GetString(5),
                        reader.GetString(6),
                        ReadDateTimeOffset(reader, 7)));
                }
            }

            await transaction.CommitAsync(cancellationToken);
            return results;
        });

    private Task<QuestionSnapshot> MutateAsync(
        Guid tenantId,
        Guid id,
        string actor,
        string action,
        string correlationId,
        Func<Question, DateTimeOffset, QuestionSnapshot> mutation,
        CancellationToken cancellationToken) => TranslateAvailabilityAsync(async () =>
    {
        var now = timeProvider.GetUtcNow();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await SetTenantAsync(connection, transaction, tenantId, cancellationToken);
        try
        {
            var question = await LoadRequiredAsync(connection, transaction, tenantId, id, cancellationToken);
            var result = mutation(question, now);
            await UpdateQuestionAsync(connection, transaction, result, cancellationToken);
            await InsertRevisionAsync(connection, transaction, result, cancellationToken);
            await InsertAuditAsync(
                connection,
                transaction,
                tenantId,
                actor,
                id,
                action,
                "success",
                correlationId,
                now,
                cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return result;
        }
        catch (DomainRuleViolationException exception)
        {
            await InsertAuditAsync(
                connection,
                transaction,
                tenantId,
                actor,
                id,
                action,
                $"rejected:{exception.Code}",
                correlationId,
                now,
                cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            throw;
        }
    });

    private static async Task<Question> LoadRequiredAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid tenantId,
        Guid id,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            $"{SelectColumns} WHERE tenant_id = @tenant_id AND id = @id FOR UPDATE;",
            connection,
            transaction);
        command.Parameters.AddWithValue("tenant_id", tenantId);
        command.Parameters.AddWithValue("id", id);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            throw new DomainRuleViolationException("question.not_found", "Question was not found.");
        }

        return Question.Rehydrate(ReadSnapshot(reader));
    }

    private static async Task InsertQuestionAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        QuestionSnapshot snapshot,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            """
            INSERT INTO questions (
                id, tenant_id, title, body, tags, status, version, owner_subject,
                created_at, updated_at, published_at, review_reason,
                withdrawal_reason, approved_version, approved_by)
            VALUES (
                @id, @tenant_id, @title, @body, @tags, @status, @version, @owner_subject,
                @created_at, @updated_at, @published_at, @review_reason,
                @withdrawal_reason, @approved_version, @approved_by);
            """,
            connection,
            transaction);
        AddQuestionParameters(command, snapshot);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task UpdateQuestionAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        QuestionSnapshot snapshot,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            """
            UPDATE questions
            SET title = @title,
                body = @body,
                tags = @tags,
                status = @status,
                version = @version,
                owner_subject = @owner_subject,
                created_at = @created_at,
                updated_at = @updated_at,
                published_at = @published_at,
                review_reason = @review_reason,
                withdrawal_reason = @withdrawal_reason,
                approved_version = @approved_version,
                approved_by = @approved_by,
                published_revision_id = CASE
                    WHEN @approved_version IS NULL THEN NULL
                    ELSE (
                        SELECT revision.id
                        FROM question_revisions AS revision
                        WHERE revision.tenant_id = @tenant_id
                          AND revision.question_id = @id
                          AND revision.version = @approved_version)
                END
            WHERE tenant_id = @tenant_id AND id = @id;
            """,
            connection,
            transaction);
        AddQuestionParameters(command, snapshot);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static void AddQuestionParameters(NpgsqlCommand command, QuestionSnapshot snapshot)
    {
        command.Parameters.AddWithValue("id", snapshot.Id);
        command.Parameters.AddWithValue("tenant_id", snapshot.TenantId);
        command.Parameters.AddWithValue("title", snapshot.Title);
        command.Parameters.AddWithValue("body", snapshot.Body);
        command.Parameters.AddWithValue("tags", snapshot.Tags.ToArray());
        command.Parameters.AddWithValue("status", ToDatabaseStatus(snapshot.Status));
        command.Parameters.AddWithValue("version", snapshot.Version);
        command.Parameters.AddWithValue("owner_subject", snapshot.OwnerSubject);
        command.Parameters.AddWithValue("created_at", snapshot.CreatedAt);
        command.Parameters.AddWithValue("updated_at", snapshot.UpdatedAt);
        command.Parameters.AddWithValue("published_at", (object?)snapshot.PublishedAt ?? DBNull.Value);
        command.Parameters.AddWithValue("review_reason", (object?)snapshot.ReviewReason ?? DBNull.Value);
        command.Parameters.AddWithValue("withdrawal_reason", (object?)snapshot.WithdrawalReason ?? DBNull.Value);
        command.Parameters.AddWithValue(
            "approved_version",
            NpgsqlDbType.Integer,
            (object?)snapshot.ApprovedVersion ?? DBNull.Value);
        command.Parameters.AddWithValue("approved_by", (object?)snapshot.ApprovedBy ?? DBNull.Value);
    }

    private static async Task InsertRevisionAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        QuestionSnapshot snapshot,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            """
            INSERT INTO question_revisions (
                tenant_id, id, question_id, version, title, body, tags, status,
                owner_subject, created_at, recorded_at, published_at, review_reason,
                withdrawal_reason, approved_version, approved_by)
            VALUES (
                @tenant_id, @revision_id, @id, @version, @title, @body, @tags, @status,
                @owner_subject, @created_at, @updated_at, @published_at, @review_reason,
                @withdrawal_reason, @approved_version, @approved_by);
            """,
            connection,
            transaction);
        AddQuestionParameters(command, snapshot);
        command.Parameters.AddWithValue("revision_id", Guid.NewGuid());
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task InsertAuditAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid tenantId,
        string actor,
        Guid targetId,
        string action,
        string result,
        string correlationId,
        DateTimeOffset occurredAt,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            """
            INSERT INTO audit_events (
                id, tenant_id, actor_subject, target_id, action, result, correlation_id, occurred_at)
            VALUES (@id, @tenant_id, @actor, @target_id, @action, @result, @correlation_id, @occurred_at);
            """,
            connection,
            transaction);
        command.Parameters.AddWithValue("id", Guid.NewGuid());
        command.Parameters.AddWithValue("tenant_id", tenantId);
        command.Parameters.AddWithValue("actor", actor);
        command.Parameters.AddWithValue("target_id", targetId);
        command.Parameters.AddWithValue("action", action);
        command.Parameters.AddWithValue("result", result);
        command.Parameters.AddWithValue("correlation_id", correlationId);
        command.Parameters.AddWithValue("occurred_at", occurredAt);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<(string Fingerprint, QuestionSnapshot Result)?> ReadIdempotencyAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid tenantId,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            """
            SELECT fingerprint, response_snapshot::text
            FROM idempotency_records
            WHERE tenant_id = @tenant_id AND idempotency_key = @key;
            """,
            connection,
            transaction);
        command.Parameters.AddWithValue("tenant_id", tenantId);
        command.Parameters.AddWithValue("key", idempotencyKey);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        var result = JsonSerializer.Deserialize<QuestionSnapshot>(reader.GetString(1), SnapshotJsonOptions)
            ?? throw new InvalidOperationException("Stored idempotency response was empty.");
        return (reader.GetString(0), result);
    }

    private static async Task DeleteExpiredIdempotencyAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid tenantId,
        string idempotencyKey,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            """
            DELETE FROM idempotency_records
            WHERE tenant_id = @tenant_id
              AND idempotency_key = @key
              AND expires_at <= @now;
            """,
            connection,
            transaction);
        command.Parameters.AddWithValue("tenant_id", tenantId);
        command.Parameters.AddWithValue("key", idempotencyKey);
        command.Parameters.AddWithValue("now", now);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task InsertIdempotencyAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid tenantId,
        string idempotencyKey,
        string actor,
        int expectedVersion,
        string fingerprint,
        QuestionSnapshot result,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            """
            INSERT INTO idempotency_records (
                tenant_id, idempotency_key, actor_subject, expected_version,
                fingerprint, response_snapshot, created_at, expires_at)
            VALUES (
                @tenant_id, @key, @actor, @expected_version,
                @fingerprint, CAST(@snapshot AS jsonb), @created_at, @expires_at);
            """,
            connection,
            transaction);
        command.Parameters.AddWithValue("tenant_id", tenantId);
        command.Parameters.AddWithValue("key", idempotencyKey);
        command.Parameters.AddWithValue("actor", actor);
        command.Parameters.AddWithValue("expected_version", expectedVersion);
        command.Parameters.AddWithValue("fingerprint", fingerprint);
        command.Parameters.AddWithValue("snapshot", JsonSerializer.Serialize(result, SnapshotJsonOptions));
        command.Parameters.AddWithValue("created_at", now);
        command.Parameters.AddWithValue("expires_at", now.AddHours(24));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static QuestionSnapshot ReadSnapshot(NpgsqlDataReader reader) => new(
        reader.GetGuid(0),
        reader.GetString(1),
        reader.GetString(2),
        reader.GetFieldValue<string[]>(3),
        FromDatabaseStatus(reader.GetString(4)),
        reader.GetInt32(5),
        reader.GetString(6),
        ReadDateTimeOffset(reader, 7),
        ReadDateTimeOffset(reader, 8),
        reader.IsDBNull(9) ? null : ReadDateTimeOffset(reader, 9),
        reader.IsDBNull(10) ? null : reader.GetString(10),
        reader.GetGuid(11),
        reader.IsDBNull(12) ? null : reader.GetString(12),
        reader.IsDBNull(13) ? null : reader.GetInt32(13),
        reader.IsDBNull(14) ? null : reader.GetString(14));

    private static async Task SetTenantAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        if (tenantId == Guid.Empty)
        {
            throw new ArgumentException("Tenant ID must not be empty.", nameof(tenantId));
        }

        await using var command = new NpgsqlCommand(
            "SELECT set_config('app.tenant_id', @tenant_id, true);",
            connection,
            transaction);
        command.Parameters.AddWithValue("tenant_id", tenantId.ToString("D"));
        await command.ExecuteScalarAsync(cancellationToken);
    }

    private static DateTimeOffset ReadDateTimeOffset(NpgsqlDataReader reader, int ordinal) =>
        new(reader.GetDateTime(ordinal).ToUniversalTime());

    private static string ToDatabaseStatus(QuestionStatus status) => status switch
    {
        QuestionStatus.Draft => "DRAFT",
        QuestionStatus.InReview => "IN_REVIEW",
        QuestionStatus.Published => "PUBLISHED",
        QuestionStatus.Withdrawn => "WITHDRAWN",
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Unknown question status.")
    };

    private static QuestionStatus FromDatabaseStatus(string status) => status switch
    {
        "DRAFT" => QuestionStatus.Draft,
        "IN_REVIEW" => QuestionStatus.InReview,
        "PUBLISHED" => QuestionStatus.Published,
        "WITHDRAWN" => QuestionStatus.Withdrawn,
        _ => throw new InvalidOperationException($"Unknown persisted question status: {status}")
    };

    private static string EscapeLike(string value) => value
        .Replace("\\", "\\\\", StringComparison.Ordinal)
        .Replace("%", "\\%", StringComparison.Ordinal)
        .Replace("_", "\\_", StringComparison.Ordinal);

    private async Task<T> TranslateAvailabilityAsync<T>(Func<Task<T>> action)
    {
        try
        {
            return await action();
        }
        catch (Exception exception) when (IsAvailabilityFailure(exception))
        {
            LogAvailabilityFailure(exception);
            throw new StoreUnavailableException(exception);
        }
    }

    private async Task TranslateAvailabilityAsync(Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (Exception exception) when (IsAvailabilityFailure(exception))
        {
            LogAvailabilityFailure(exception);
            throw new StoreUnavailableException(exception);
        }
    }

    private void LogAvailabilityFailure(Exception exception)
    {
        var postgresException = exception as PostgresException;
        DatabaseFailureLog(
            logger,
            exception is TimeoutException ? "timeout" : "provider",
            postgresException?.SqlState ?? "not-available",
            postgresException?.TableName ?? "not-available",
            postgresException?.ConstraintName ?? "not-available",
            null);
    }

    private static bool IsAvailabilityFailure(Exception exception) =>
        exception is NpgsqlException or TimeoutException;

    private const string SelectColumns = """
        SELECT id, title, body, tags, status, version, owner_subject,
               created_at, updated_at, published_at, review_reason, tenant_id,
               withdrawal_reason, approved_version, approved_by
        FROM questions
        """;
}
