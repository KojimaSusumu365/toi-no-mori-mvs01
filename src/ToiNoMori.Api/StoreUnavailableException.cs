namespace ToiNoMori.Api;

public sealed class StoreUnavailableException : Exception
{
    public StoreUnavailableException(Exception innerException)
        : base("The persistence service is temporarily unavailable.", innerException)
    {
    }
}
