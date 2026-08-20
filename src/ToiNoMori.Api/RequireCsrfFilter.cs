using System.Security.Cryptography;
using System.Text;

namespace ToiNoMori.Api;

public sealed class RequireCsrfFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var expected = context.HttpContext.User.FindFirst("csrf")?.Value;
        var supplied = context.HttpContext.Request.Headers["X-CSRF-Token"].ToString();
        if (string.IsNullOrWhiteSpace(supplied) && context.HttpContext.Request.HasFormContentType)
        {
            var form = await context.HttpContext.Request.ReadFormAsync(context.HttpContext.RequestAborted);
            supplied = form["csrfToken"].ToString();
        }

        if (string.IsNullOrWhiteSpace(expected)
            || string.IsNullOrWhiteSpace(supplied)
            || !FixedTimeEquals(expected, supplied))
        {
            return Results.Problem(
                statusCode: StatusCodes.Status403Forbidden,
                title: "CSRF validation failed",
                type: "https://toi-no-mori.example/problems/csrf");
        }

        return await next(context);
    }

    private static bool FixedTimeEquals(string expected, string supplied)
    {
        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        var suppliedBytes = Encoding.UTF8.GetBytes(supplied);
        return expectedBytes.Length == suppliedBytes.Length
            && CryptographicOperations.FixedTimeEquals(expectedBytes, suppliedBytes);
    }
}
