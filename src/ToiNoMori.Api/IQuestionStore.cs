using ToiNoMori.Domain;

namespace ToiNoMori.Api;

public interface IQuestionStore
{
    Task InitializeAsync(CancellationToken cancellationToken);

    Task<bool> IsReadyAsync(CancellationToken cancellationToken);

    Task<QuestionSnapshot> CreateAsync(
        Guid tenantId,
        ValidatedQuestionContent content,
        string actor,
        string correlationId,
        CancellationToken cancellationToken);

    Task<QuestionSnapshot> UpdateAsync(
        Guid tenantId,
        Guid id,
        ValidatedQuestionContent content,
        int expectedVersion,
        string actor,
        string correlationId,
        CancellationToken cancellationToken);

    Task<QuestionSnapshot> SubmitAsync(
        Guid tenantId,
        Guid id,
        string actor,
        string correlationId,
        CancellationToken cancellationToken);

    Task<QuestionSnapshot> ReturnForChangesAsync(
        Guid tenantId,
        Guid id,
        string reviewer,
        string reason,
        string correlationId,
        CancellationToken cancellationToken);

    Task<QuestionSnapshot> ApproveAsync(
        Guid tenantId,
        Guid id,
        string reviewer,
        int expectedVersion,
        string idempotencyKey,
        string correlationId,
        CancellationToken cancellationToken);

    Task<QuestionSnapshot> WithdrawAsync(
        Guid tenantId,
        Guid id,
        string actor,
        string reason,
        string correlationId,
        CancellationToken cancellationToken);

    Task<QuestionSnapshot?> FindAdministrativeAsync(
        Guid tenantId,
        Guid id,
        string actor,
        bool isReviewer,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<QuestionSnapshot>> SearchAdministrativeAsync(
        Guid tenantId,
        string actor,
        bool isReviewer,
        QuestionStatus? status,
        int limit,
        CancellationToken cancellationToken);

    Task<QuestionSnapshot?> FindPublicAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<QuestionSnapshot>> SearchPublicAsync(
        string? query,
        string? tag,
        int limit,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<AuditRecord>> ReadAuditAsync(
        Guid tenantId,
        CancellationToken cancellationToken);
}
