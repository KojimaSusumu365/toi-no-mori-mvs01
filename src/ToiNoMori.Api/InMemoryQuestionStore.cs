using ToiNoMori.Domain;

namespace ToiNoMori.Api;

/// <summary>
/// MVS-01 の実行可能な最小リポジトリ。
/// 次反復で同じ公開契約を PostgreSQL 実装へ差し替える。
/// </summary>
public sealed class InMemoryQuestionStore(TimeProvider timeProvider) : IQuestionStore
{
    private readonly object _gate = new();
    private readonly Dictionary<Guid, Question> _questions = [];
    private readonly Dictionary<(Guid TenantId, string Key), IdempotencyEntry> _idempotency = [];
    private readonly List<AuditRecord> _audit = [];

    public QuestionSnapshot Create(
        Guid tenantId,
        ValidatedQuestionContent content,
        string actor,
        string correlationId)
    {
        lock (_gate)
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
            _questions.Add(question.Id, question);
            AddAudit(tenantId, actor, question.Id, "question.create", "success", correlationId, now);
            return question.Snapshot();
        }
    }

    public QuestionSnapshot Update(
        Guid tenantId,
        Guid id,
        ValidatedQuestionContent content,
        int expectedVersion,
        string actor,
        string correlationId)
    {
        lock (_gate)
        {
            var question = GetRequired(tenantId, id);
            try
            {
                question.Update(content.Title, content.Body, content.Tags, expectedVersion, actor, timeProvider.GetUtcNow());
                AddAudit(tenantId, actor, id, "question.update", "success", correlationId, timeProvider.GetUtcNow());
                return question.Snapshot();
            }
            catch (DomainRuleViolationException exception)
            {
                AddAudit(tenantId, actor, id, "question.update", $"rejected:{exception.Code}", correlationId, timeProvider.GetUtcNow());
                throw;
            }
        }
    }

    public QuestionSnapshot Submit(Guid tenantId, Guid id, string actor, string correlationId)
    {
        lock (_gate)
        {
            var question = GetRequired(tenantId, id);
            try
            {
                question.Submit(actor, timeProvider.GetUtcNow());
                AddAudit(tenantId, actor, id, "question.submit", "success", correlationId, timeProvider.GetUtcNow());
                return question.Snapshot();
            }
            catch (DomainRuleViolationException exception)
            {
                AddAudit(tenantId, actor, id, "question.submit", $"rejected:{exception.Code}", correlationId, timeProvider.GetUtcNow());
                throw;
            }
        }
    }

    public QuestionSnapshot ReturnForChanges(
        Guid tenantId,
        Guid id,
        string reviewer,
        string reason,
        string correlationId)
    {
        lock (_gate)
        {
            var question = GetRequired(tenantId, id);
            question.ReturnForChanges(reviewer, reason, timeProvider.GetUtcNow());
            AddAudit(tenantId, reviewer, id, "question.return", "success", correlationId, timeProvider.GetUtcNow());
            return question.Snapshot();
        }
    }

    public QuestionSnapshot Approve(
        Guid tenantId,
        Guid id,
        string reviewer,
        int expectedVersion,
        string idempotencyKey,
        string correlationId)
    {
        lock (_gate)
        {
            var now = timeProvider.GetUtcNow();
            var fingerprint = $"question.approve:{tenantId}:{id}:{reviewer}:{expectedVersion}";
            var scopedKey = (tenantId, idempotencyKey);
            if (_idempotency.TryGetValue(scopedKey, out var existing))
            {
                if (existing.ExpiresAt <= now)
                {
                    _idempotency.Remove(scopedKey);
                }
                else
                {
                    if (!string.Equals(existing.Fingerprint, fingerprint, StringComparison.Ordinal))
                    {
                        throw new DomainRuleViolationException(
                            "idempotency.key_reused",
                            "The idempotency key was already used for a different command.");
                    }

                    return existing.Result;
                }
            }

            var question = GetRequired(tenantId, id);
            try
            {
                question.Approve(reviewer, expectedVersion, now);
                var result = question.Snapshot();
                _idempotency.Add(scopedKey, new(fingerprint, result, now.AddHours(24)));
                AddAudit(tenantId, reviewer, id, "question.approve", "success", correlationId, now);
                return result;
            }
            catch (DomainRuleViolationException exception)
            {
                AddAudit(tenantId, reviewer, id, "question.approve", $"rejected:{exception.Code}", correlationId, timeProvider.GetUtcNow());
                throw;
            }
        }
    }

    public QuestionSnapshot Withdraw(
        Guid tenantId,
        Guid id,
        string actor,
        string reason,
        string correlationId)
    {
        lock (_gate)
        {
            var question = GetRequired(tenantId, id);
            question.Withdraw(actor, reason, timeProvider.GetUtcNow());
            AddAudit(tenantId, actor, id, "question.withdraw", "success", correlationId, timeProvider.GetUtcNow());
            return question.Snapshot();
        }
    }

    public QuestionSnapshot? FindAdministrative(Guid tenantId, Guid id, string actor, bool isReviewer)
    {
        lock (_gate)
        {
            return _questions.TryGetValue(id, out var question)
                && question.TenantId == tenantId
                && (isReviewer || string.Equals(question.OwnerSubject, actor, StringComparison.Ordinal))
                    ? question.Snapshot()
                    : null;
        }
    }

    public IReadOnlyList<QuestionSnapshot> SearchAdministrative(
        Guid tenantId,
        string actor,
        bool isReviewer,
        QuestionStatus? status,
        int limit)
    {
        lock (_gate)
        {
            return _questions.Values
                .Where(question => question.TenantId == tenantId)
                .Where(question => isReviewer
                    || string.Equals(question.OwnerSubject, actor, StringComparison.Ordinal))
                .Where(question => status is null || question.Status == status)
                .OrderByDescending(question => question.UpdatedAt)
                .ThenBy(question => question.Id)
                .Take(Math.Clamp(limit, 1, 100))
                .Select(question => question.Snapshot())
                .ToArray();
        }
    }

    public QuestionSnapshot? FindPublic(Guid id)
    {
        lock (_gate)
        {
            return _questions.TryGetValue(id, out var question)
                && question.TenantId == TenantIds.Mvs01
                && question.Status == QuestionStatus.Published
                ? question.Snapshot()
                : null;
        }
    }

    public IReadOnlyList<QuestionSnapshot> SearchPublic(string? query, string? tag, int limit)
    {
        lock (_gate)
        {
            var normalizedQuery = query?.Trim();
            var normalizedTag = tag?.Trim();

            return _questions.Values
                .Where(question => question.TenantId == TenantIds.Mvs01
                    && question.Status == QuestionStatus.Published)
                .Where(question => string.IsNullOrWhiteSpace(normalizedQuery)
                    || question.Title.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase)
                    || question.Body.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase)
                    || question.Tags.Any(value => value.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase)))
                .Where(question => string.IsNullOrWhiteSpace(normalizedTag)
                    || question.Tags.Contains(normalizedTag, StringComparer.OrdinalIgnoreCase))
                .OrderByDescending(question => question.PublishedAt)
                .ThenBy(question => question.Id)
                .Take(Math.Clamp(limit, 1, 50))
                .Select(question => question.Snapshot())
                .ToArray();
        }
    }

    public IReadOnlyList<AuditRecord> ReadAudit()
    {
        lock (_gate)
        {
            return [.. _audit];
        }
    }

    public IReadOnlyList<AuditRecord> ReadAudit(Guid tenantId)
    {
        lock (_gate)
        {
            return _audit.Where(record => record.TenantId == tenantId).ToArray();
        }
    }

    private Question GetRequired(Guid tenantId, Guid id) =>
        _questions.TryGetValue(id, out var question) && question.TenantId == tenantId
        ? question
        : throw new DomainRuleViolationException("question.not_found", "Question was not found.");

    private void AddAudit(
        Guid tenantId,
        string actor,
        Guid targetId,
        string action,
        string result,
        string correlationId,
        DateTimeOffset occurredAt)
    {
        _audit.Add(new(
            Guid.NewGuid(),
            tenantId,
            actor,
            targetId,
            action,
            result,
            correlationId,
            occurredAt));
    }

    private sealed record IdempotencyEntry(
        string Fingerprint,
        QuestionSnapshot Result,
        DateTimeOffset ExpiresAt);

    Task IQuestionStore.InitializeAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    Task<bool> IQuestionStore.IsReadyAsync(CancellationToken cancellationToken) => Task.FromResult(true);

    Task<QuestionSnapshot> IQuestionStore.CreateAsync(
        Guid tenantId,
        ValidatedQuestionContent content,
        string actor,
        string correlationId,
        CancellationToken cancellationToken) => Task.FromResult(Create(tenantId, content, actor, correlationId));

    Task<QuestionSnapshot> IQuestionStore.UpdateAsync(
        Guid tenantId,
        Guid id,
        ValidatedQuestionContent content,
        int expectedVersion,
        string actor,
        string correlationId,
        CancellationToken cancellationToken) =>
        Task.FromResult(Update(tenantId, id, content, expectedVersion, actor, correlationId));

    Task<QuestionSnapshot> IQuestionStore.SubmitAsync(
        Guid tenantId,
        Guid id,
        string actor,
        string correlationId,
        CancellationToken cancellationToken) => Task.FromResult(Submit(tenantId, id, actor, correlationId));

    Task<QuestionSnapshot> IQuestionStore.ReturnForChangesAsync(
        Guid tenantId,
        Guid id,
        string reviewer,
        string reason,
        string correlationId,
        CancellationToken cancellationToken) =>
        Task.FromResult(ReturnForChanges(tenantId, id, reviewer, reason, correlationId));

    Task<QuestionSnapshot> IQuestionStore.ApproveAsync(
        Guid tenantId,
        Guid id,
        string reviewer,
        int expectedVersion,
        string idempotencyKey,
        string correlationId,
        CancellationToken cancellationToken) =>
        Task.FromResult(Approve(tenantId, id, reviewer, expectedVersion, idempotencyKey, correlationId));

    Task<QuestionSnapshot> IQuestionStore.WithdrawAsync(
        Guid tenantId,
        Guid id,
        string actor,
        string reason,
        string correlationId,
        CancellationToken cancellationToken) =>
        Task.FromResult(Withdraw(tenantId, id, actor, reason, correlationId));

    Task<QuestionSnapshot?> IQuestionStore.FindAdministrativeAsync(
        Guid tenantId,
        Guid id,
        string actor,
        bool isReviewer,
        CancellationToken cancellationToken) => Task.FromResult(FindAdministrative(tenantId, id, actor, isReviewer));

    Task<IReadOnlyList<QuestionSnapshot>> IQuestionStore.SearchAdministrativeAsync(
        Guid tenantId,
        string actor,
        bool isReviewer,
        QuestionStatus? status,
        int limit,
        CancellationToken cancellationToken) =>
        Task.FromResult(SearchAdministrative(tenantId, actor, isReviewer, status, limit));

    Task<QuestionSnapshot?> IQuestionStore.FindPublicAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(FindPublic(id));

    Task<IReadOnlyList<QuestionSnapshot>> IQuestionStore.SearchPublicAsync(
        string? query,
        string? tag,
        int limit,
        CancellationToken cancellationToken) => Task.FromResult(SearchPublic(query, tag, limit));

    Task<IReadOnlyList<AuditRecord>> IQuestionStore.ReadAuditAsync(
        Guid tenantId,
        CancellationToken cancellationToken) => Task.FromResult(ReadAudit(tenantId));
}
