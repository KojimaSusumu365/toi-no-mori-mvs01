using ToiNoMori.Domain;

namespace ToiNoMori.Api;

public sealed record QuestionContentRequest(string? Title, string? Body, IReadOnlyList<string?>? Tags);

public sealed record ReviewReasonRequest(string? Reason);

public sealed record QuestionResponse(
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
    string? ReviewReason)
{
    public static QuestionResponse From(QuestionSnapshot value) => new(
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
        value.ReviewReason);
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
