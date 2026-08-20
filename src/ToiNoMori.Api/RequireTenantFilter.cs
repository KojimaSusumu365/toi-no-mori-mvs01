namespace ToiNoMori.Api;

public sealed class RequireTenantFilter(TenantResolver resolver) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        try
        {
            context.HttpContext.Items[TenantResolver.HttpContextItemName] =
                resolver.Resolve(context.HttpContext.User);
            return await next(context);
        }
        catch (TenantResolutionException exception)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status403Forbidden,
                title: "Tenant context could not be established.",
                type: $"https://toi-no-mori.example/problems/{exception.Code.Replace('.', '-').Replace('_', '-')}");
        }
    }
}
