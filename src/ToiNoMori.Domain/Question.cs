namespace ToiNoMori.Domain;

/// <summary>
/// UML-CLS-MVS01-001 の Question 集約ルート。
/// REQ-MVS01-QST-001/002、WF-001/002、WD-001 の状態規則を一か所で守る。
/// </summary>
public sealed class Question
{
    private readonly List<string> _tags;

    private Question(QuestionSnapshot snapshot)
    {
        Id = snapshot.Id;
        TenantId = snapshot.TenantId;
        Title = snapshot.Title;
        Body = snapshot.Body;
        _tags = [.. snapshot.Tags];
        Status = snapshot.Status;
        Version = snapshot.Version;
        OwnerSubject = snapshot.OwnerSubject;
        CreatedAt = snapshot.CreatedAt;
        UpdatedAt = snapshot.UpdatedAt;
        PublishedAt = snapshot.PublishedAt;
        ReviewReason = snapshot.ReviewReason;
        WithdrawalReason = snapshot.WithdrawalReason;
        ApprovedVersion = snapshot.ApprovedVersion;
        ApprovedBy = snapshot.ApprovedBy;
    }

    public Question(
        Guid id,
        string title,
        string body,
        IEnumerable<string> tags,
        string ownerSubject,
        DateTimeOffset now)
        : this(id, TenantIds.Mvs01, title, body, tags, ownerSubject, now)
    {
    }

    public Question(
        Guid id,
        Guid tenantId,
        string title,
        string body,
        IEnumerable<string> tags,
        string ownerSubject,
        DateTimeOffset now)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("ID must not be empty.", nameof(id));
        }

        if (tenantId == Guid.Empty)
        {
            throw new ArgumentException("Tenant ID must not be empty.", nameof(tenantId));
        }

        if (string.IsNullOrWhiteSpace(ownerSubject))
        {
            throw new ArgumentException("Owner subject is required.", nameof(ownerSubject));
        }

        Id = id;
        TenantId = tenantId;
        Title = title;
        Body = body;
        _tags = [.. tags];
        OwnerSubject = ownerSubject;
        Status = QuestionStatus.Draft;
        Version = 1;
        CreatedAt = now;
        UpdatedAt = now;
    }

    public Guid Id { get; }
    public Guid TenantId { get; }
    public string Title { get; private set; }
    public string Body { get; private set; }
    public IReadOnlyList<string> Tags => _tags;
    public QuestionStatus Status { get; private set; }
    public int Version { get; private set; }
    public string OwnerSubject { get; }
    public DateTimeOffset CreatedAt { get; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public DateTimeOffset? PublishedAt { get; private set; }
    public string? ReviewReason { get; private set; }
    public string? WithdrawalReason { get; private set; }
    public int? ApprovedVersion { get; private set; }
    public string? ApprovedBy { get; private set; }

    public static Question Rehydrate(QuestionSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return new(snapshot);
    }

    public void Update(
        string title,
        string body,
        IEnumerable<string> tags,
        int expectedVersion,
        string actorSubject,
        DateTimeOffset now)
    {
        EnsureOwner(actorSubject);
        EnsureState(QuestionStatus.Draft, "question.update.invalid_state");
        EnsureVersion(expectedVersion);

        Title = title;
        Body = body;
        _tags.Clear();
        _tags.AddRange(tags);
        ReviewReason = null;
        Advance(now);
    }

    public void Submit(string actorSubject, DateTimeOffset now)
    {
        EnsureOwner(actorSubject);
        EnsureState(QuestionStatus.Draft, "question.submit.invalid_state");
        Status = QuestionStatus.InReview;
        ReviewReason = null;
        Advance(now);
    }

    public void ReturnForChanges(string reviewerSubject, string reason, DateTimeOffset now)
    {
        EnsureState(QuestionStatus.InReview, "question.return.invalid_state");
        if (string.IsNullOrWhiteSpace(reviewerSubject))
        {
            throw new DomainRuleViolationException("question.reviewer.required", "Reviewer subject is required.");
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new DomainRuleViolationException("question.return.reason_required", "A return reason is required.");
        }

        Status = QuestionStatus.Draft;
        ReviewReason = reason.Trim();
        Advance(now);
    }

    public void Approve(string reviewerSubject, DateTimeOffset now) => Approve(reviewerSubject, Version, now);

    public void Approve(string reviewerSubject, int expectedVersion, DateTimeOffset now)
    {
        EnsureState(QuestionStatus.InReview, "question.approve.invalid_state");
        if (string.IsNullOrWhiteSpace(reviewerSubject))
        {
            throw new DomainRuleViolationException("question.reviewer.required", "Reviewer subject is required.");
        }

        if (string.Equals(OwnerSubject, reviewerSubject, StringComparison.Ordinal))
        {
            throw new DomainRuleViolationException("question.approve.self_forbidden", "The owner cannot approve their own question.");
        }

        EnsureVersion(expectedVersion);

        ApprovedVersion = Version;
        ApprovedBy = reviewerSubject;
        Status = QuestionStatus.Published;
        PublishedAt = now;
        ReviewReason = null;
        Advance(now);
    }

    public void Withdraw(string actorSubject, string reason, DateTimeOffset now)
    {
        EnsureState(QuestionStatus.Published, "question.withdraw.invalid_state");
        if (string.IsNullOrWhiteSpace(actorSubject))
        {
            throw new DomainRuleViolationException("question.actor.required", "Actor subject is required.");
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new DomainRuleViolationException("question.withdrawal.reason_required", "A withdrawal reason is required.");
        }

        Status = QuestionStatus.Withdrawn;
        ReviewReason = null;
        WithdrawalReason = reason.Trim();
        Advance(now);
    }

    public QuestionSnapshot Snapshot() => new(
        Id,
        Title,
        Body,
        [.. _tags],
        Status,
        Version,
        OwnerSubject,
        CreatedAt,
        UpdatedAt,
        PublishedAt,
        ReviewReason,
        TenantId,
        WithdrawalReason,
        ApprovedVersion,
        ApprovedBy);

    private void EnsureOwner(string actorSubject)
    {
        if (!string.Equals(OwnerSubject, actorSubject, StringComparison.Ordinal))
        {
            throw new DomainRuleViolationException("question.owner.forbidden", "Only the owner may perform this operation.");
        }
    }

    private void EnsureVersion(int expectedVersion)
    {
        if (Version != expectedVersion)
        {
            throw new DomainRuleViolationException(
                "question.version.conflict",
                $"Expected version {expectedVersion}, but the current version is {Version}.");
        }
    }

    private void EnsureState(QuestionStatus expected, string code)
    {
        if (Status != expected)
        {
            throw new DomainRuleViolationException(code, $"Operation requires {expected}, but the current state is {Status}.");
        }
    }

    private void Advance(DateTimeOffset now)
    {
        Version++;
        UpdatedAt = now;
    }
}
