namespace ToiNoMori.Testing;

public sealed record SpecTest(
    string TestCaseId,
    string RequirementId,
    string Name,
    Func<Task> Execute);
