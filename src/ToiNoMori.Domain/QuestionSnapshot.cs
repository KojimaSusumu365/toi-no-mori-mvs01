namespace ToiNoMori.Domain;

/// <summary>
/// Question 集約を永続化境界へ渡す不変スナップショット。
/// 末尾の省略可能引数は Stage 6R-1 の保存形式を読み戻すための互換境界である。
/// </summary>
public sealed record QuestionSnapshot
{
    public QuestionSnapshot(
        Guid id,
        string title,
        string body,
        IReadOnlyList<string> tags,
        QuestionStatus status,
        int version,
        string ownerSubject,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt,
        DateTimeOffset? publishedAt,
        string? reviewReason,
        Guid tenantId = default,
        string? withdrawalReason = null,
        int? approvedVersion = null,
        string? approvedBy = null)
    {
        Id = id;
        Title = title;
        Body = body;
        Tags = tags;
        Status = status;
        Version = version;
        OwnerSubject = ownerSubject;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
        PublishedAt = publishedAt;
        ReviewReason = reviewReason;
        TenantId = tenantId == Guid.Empty ? TenantIds.Mvs01 : tenantId;
        WithdrawalReason = withdrawalReason;
        ApprovedVersion = approvedVersion;
        ApprovedBy = approvedBy;
    }

    public Guid Id { get; }
    public string Title { get; }
    public string Body { get; }
    public IReadOnlyList<string> Tags { get; }
    public QuestionStatus Status { get; }
    public int Version { get; }
    public string OwnerSubject { get; }
    public DateTimeOffset CreatedAt { get; }
    public DateTimeOffset UpdatedAt { get; }
    public DateTimeOffset? PublishedAt { get; }
    public string? ReviewReason { get; }
    public Guid TenantId { get; }
    public string? WithdrawalReason { get; }
    public int? ApprovedVersion { get; }
    public string? ApprovedBy { get; }
}
