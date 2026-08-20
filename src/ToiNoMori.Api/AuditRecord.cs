namespace ToiNoMori.Api;

public sealed record AuditRecord(
    Guid Id,
    Guid TenantId,
    string ActorSubject,
    Guid TargetId,
    string Action,
    string Result,
    string CorrelationId,
    DateTimeOffset OccurredAt);
