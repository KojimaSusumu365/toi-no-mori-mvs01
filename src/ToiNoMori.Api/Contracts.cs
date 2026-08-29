using ToiNoMori.Domain;

namespace ToiNoMori.Api;

public sealed record QuestionContentRequest(string? Title, string? Body, IReadOnlyList<string?>? Tags);

public sealed record ReviewReasonRequest(string? Reason);

public sealed record EditorQuestionResponse(
    Guid Id,
    string Title,
    string Body,
    IReadOnlyList<string> Tags,
    QuestionStatus Status,
    int Version,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? PublishedAt,
    string? ReviewReason)
{
    public static EditorQuestionResponse From(QuestionSnapshot value) => new(
        value.Id,
        value.Title,
        value.Body,
        value.Tags,
        value.Status,
        value.Version,
        value.CreatedAt,
        value.UpdatedAt,
        value.PublishedAt,
        value.ReviewReason);
}

public sealed record ReviewerQuestionResponse(
    Guid Id,
    string Title,
    string Body,
    IReadOnlyList<string> Tags,
    QuestionStatus Status,
    int Version,
    string OwnerSubject,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? PublishedAt,
    string? ReviewReason,
    string? WithdrawalReason)
{
    public static ReviewerQuestionResponse From(QuestionSnapshot value) => new(
        value.Id,
        value.Title,
        value.Body,
        value.Tags,
        value.Status,
        value.Version,
        value.OwnerSubject,
        value.CreatedAt,
        value.UpdatedAt,
        value.PublishedAt,
        value.ReviewReason,
        value.WithdrawalReason);
}

public sealed record PublicQuestionResponse(
    Guid Id,
    string Title,
    string Body,
    IReadOnlyList<string> Tags,
    DateTimeOffset PublishedAt)
{
    public static PublicQuestionResponse? From(QuestionSnapshot value) =>
        value.Status == QuestionStatus.Published && value.PublishedAt is { } publishedAt
            ? new(value.Id, value.Title, value.Body, value.Tags, publishedAt)
            : null;
}

public sealed record AuditRecordResponse(
    string Actor,
    Guid TargetId,
    string Action,
    string Result,
    string CorrelationId,
    DateTimeOffset OccurredAt)
{
    public static AuditRecordResponse From(AuditRecord value) => new(
        value.ActorSubject,
        value.TargetId,
        value.Action,
        value.Result,
        value.CorrelationId,
        value.OccurredAt);
}
