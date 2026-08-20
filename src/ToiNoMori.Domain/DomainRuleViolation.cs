namespace ToiNoMori.Domain;

public sealed class DomainRuleViolationException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}
