namespace ToiNoMori.Api;

internal static class CorrelationContext
{
    private const string CorrelationItem = "correlation_id";
    private const string RequestItem = "request_id";

    internal static void Establish(HttpContext context, string correlationId, string requestId)
    {
        context.Items[CorrelationItem] = correlationId;
        context.Items[RequestItem] = requestId;
    }

    internal static string CorrelationId(HttpContext context) =>
        context.Items.TryGetValue(CorrelationItem, out var value) && value is string correlationId
            ? correlationId
            : context.TraceIdentifier;

    internal static string RequestId(HttpContext context) =>
        context.Items.TryGetValue(RequestItem, out var value) && value is string requestId
            ? requestId
            : context.TraceIdentifier;
}

internal sealed class CorrelationContextMiddleware(RequestDelegate next)
{
    public Task InvokeAsync(HttpContext context)
    {
        var supplied = context.Request.Headers["X-Correlation-ID"].ToString();
        var correlationId = IsSafeIdentifier(supplied)
            ? supplied
            : Guid.NewGuid().ToString("N");
        var requestId = Guid.NewGuid().ToString("N");
        context.TraceIdentifier = requestId;
        CorrelationContext.Establish(context, correlationId, requestId);
        context.Response.Headers["X-Correlation-ID"] = correlationId;
        context.Response.Headers["X-Request-ID"] = requestId;
        return next(context);
    }

    private static bool IsSafeIdentifier(string value) =>
        value.Length is > 0 and <= 64
        && value.All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_');
}
