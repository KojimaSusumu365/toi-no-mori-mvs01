using System.Globalization;
using System.Security.Claims;
using ToiNoMori.Domain;

namespace ToiNoMori.Api;

public static class ApiEndpointMappings
{
    public static void MapMvs01Endpoints(this WebApplication app)
    {
        app.MapGet("/health/live", () => Results.Ok(new { status = "live" }));
        app.MapGet("/health/ready", async (IQuestionStore store, CancellationToken cancellationToken) =>
            await store.IsReadyAsync(cancellationToken)
                ? Results.Ok(new { status = "ready" })
                : Results.Problem(
                    statusCode: StatusCodes.Status503ServiceUnavailable,
                    title: "Persistence service unavailable",
                    type: "https://toi-no-mori.example/problems/service-unavailable"));

        var admin = app.MapGroup("/api/admin/questions")
            .AddEndpointFilter<RequireTenantFilter>();

        admin.MapGet("/", SearchAdministrativeQuestions)
            .RequireAuthorization("Staff");

        admin.MapGet("/{id:guid}", FindAdministrativeQuestion)
            .RequireAuthorization("Staff");

        admin.MapPost("/", CreateQuestion)
            .RequireAuthorization("Editor")
            .AddEndpointFilter<RequireCsrfFilter>();

        admin.MapPut("/{id:guid}", UpdateQuestion)
            .RequireAuthorization("Editor")
            .AddEndpointFilter<RequireCsrfFilter>();

        admin.MapPost("/{id:guid}/submit", SubmitQuestion)
            .RequireAuthorization("Editor")
            .AddEndpointFilter<RequireCsrfFilter>();

        admin.MapPost("/{id:guid}/return", ReturnQuestion)
            .RequireAuthorization("Reviewer")
            .AddEndpointFilter<RequireCsrfFilter>();

        admin.MapPost("/{id:guid}/approve", ApproveQuestion)
            .RequireAuthorization("Reviewer")
            .AddEndpointFilter<RequireCsrfFilter>();

        admin.MapPost("/{id:guid}/withdraw", WithdrawQuestion)
            .RequireAuthorization("Reviewer")
            .AddEndpointFilter<RequireCsrfFilter>();

        app.MapGet("/api/ops/audit", (
            int? limit,
            HttpContext httpContext,
            IQuestionStore store,
            CancellationToken cancellationToken) =>
            ReadAudit(null, limit, httpContext, store, cancellationToken))
            .RequireAuthorization("Auditor")
            .AddEndpointFilter<RequireTenantFilter>();

        app.MapGet("/api/ops/audit/questions/{id:guid}", (
            Guid id,
            int? limit,
            HttpContext httpContext,
            IQuestionStore store,
            CancellationToken cancellationToken) =>
            ReadAudit(id, limit, httpContext, store, cancellationToken))
            .RequireAuthorization("Auditor")
            .AddEndpointFilter<RequireTenantFilter>();

        var publicApi = app.MapGroup("/api/public/questions")
            .RequireRateLimiting("public");

        publicApi.MapGet("/{id:guid}", async (
            Guid id,
            IQuestionStore store,
            CancellationToken cancellationToken) => await ExecuteAsync(async () =>
        {
            var question = await store.FindPublicAsync(id, cancellationToken);
            var response = question is null ? null : PublicQuestionResponse.From(question);
            return response is null ? Results.NotFound() : Results.Ok(response);
        }));

        publicApi.MapGet("/", async (
            string? query,
            string? tag,
            int? limit,
            IQuestionStore store,
            CancellationToken cancellationToken) => await ExecuteAsync(async () =>
        {
            var results = (await store.SearchPublicAsync(query, tag, limit ?? 20, cancellationToken))
                .Select(PublicQuestionResponse.From)
                .OfType<PublicQuestionResponse>()
                .ToArray();
            return Results.Ok(results);
        }));
    }

    private static Task<IResult> ReadAudit(
        Guid? targetId,
        int? limit,
        HttpContext httpContext,
        IQuestionStore store,
        CancellationToken cancellationToken)
    {
        var boundedLimit = limit ?? 50;
        if (boundedLimit is < 1 or > 200)
        {
            return Task.FromResult<IResult>(Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["limit"] = ["limit must be between 1 and 200."]
            }));
        }

        return ExecuteAsync(async () => Results.Ok(
            (await store.ReadAuditAsync(
                TenantResolver.Current(httpContext),
                targetId,
                boundedLimit,
                cancellationToken))
            .Select(AuditRecordResponse.From)
            .ToArray()));
    }

    private static Task<IResult> FindAdministrativeQuestion(
        Guid id,
        HttpContext httpContext,
        IQuestionStore store,
        CancellationToken cancellationToken) => ExecuteAsync(async () =>
    {
        var result = await store.FindAdministrativeAsync(
            TenantResolver.Current(httpContext),
            id,
            Subject(httpContext.User),
            httpContext.User.IsInRole("Reviewer"),
            cancellationToken);
        if (result is null)
        {
            return NotVisible();
        }

        httpContext.Response.Headers.ETag = QuoteVersion(result.Version);
        return Results.Ok(QuestionResponse.From(result));
    });

    private static Task<IResult> SearchAdministrativeQuestions(
        string? status,
        int? limit,
        HttpContext httpContext,
        IQuestionStore store,
        CancellationToken cancellationToken)
    {
        if (!TryParseStatus(status, out var parsedStatus))
        {
            return Task.FromResult<IResult>(Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["status"] = ["Status must be DRAFT, IN_REVIEW, PUBLISHED, or WITHDRAWN."]
            }));
        }

        return ExecuteAsync(async () =>
        {
            var results = await store.SearchAdministrativeAsync(
                TenantResolver.Current(httpContext),
                Subject(httpContext.User),
                httpContext.User.IsInRole("Reviewer"),
                parsedStatus,
                limit ?? 50,
                cancellationToken);
            return Results.Ok(results.Select(QuestionResponse.From).ToArray());
        });
    }

    private static Task<IResult> CreateQuestion(
        QuestionContentRequest request,
        HttpContext httpContext,
        IQuestionStore store,
        CancellationToken cancellationToken)
    {
        if (!QuestionInputValidator.TryValidate(request, out var content, out var errors))
        {
            return Task.FromResult<IResult>(Results.ValidationProblem(errors));
        }

        return ExecuteAsync(async () =>
        {
            var result = await store.CreateAsync(
                TenantResolver.Current(httpContext),
                content!,
                Subject(httpContext.User),
                httpContext.TraceIdentifier,
                cancellationToken);
            httpContext.Response.Headers.ETag = QuoteVersion(result.Version);
            return Results.Created($"/api/admin/questions/{result.Id}", QuestionResponse.From(result));
        });
    }

    private static Task<IResult> UpdateQuestion(
        Guid id,
        QuestionContentRequest request,
        HttpContext httpContext,
        IQuestionStore store,
        CancellationToken cancellationToken)
    {
        if (!TryParseIfMatch(httpContext.Request.Headers.IfMatch.ToString(), out var version))
        {
            return Task.FromResult<IResult>(Results.Problem(
                statusCode: StatusCodes.Status428PreconditionRequired,
                title: "If-Match is required",
                type: "https://toi-no-mori.example/problems/precondition-required"));
        }

        if (!QuestionInputValidator.TryValidate(request, out var content, out var errors))
        {
            return Task.FromResult<IResult>(Results.ValidationProblem(errors));
        }

        return ExecuteAsync(async () =>
        {
            var result = await store.UpdateAsync(
                TenantResolver.Current(httpContext),
                id,
                content!,
                version,
                Subject(httpContext.User),
                httpContext.TraceIdentifier,
                cancellationToken);
            httpContext.Response.Headers.ETag = QuoteVersion(result.Version);
            return Results.Ok(QuestionResponse.From(result));
        });
    }

    private static Task<IResult> SubmitQuestion(
        Guid id,
        HttpContext httpContext,
        IQuestionStore store,
        CancellationToken cancellationToken) => ExecuteAsync(async () => Results.Ok(QuestionResponse.From(
            await store.SubmitAsync(
                TenantResolver.Current(httpContext),
                id,
                Subject(httpContext.User),
                httpContext.TraceIdentifier,
                cancellationToken))));

    private static Task<IResult> ReturnQuestion(
        Guid id,
        ReviewReasonRequest request,
        HttpContext httpContext,
        IQuestionStore store,
        CancellationToken cancellationToken)
    {
        if (!TryValidateReviewReason(request.Reason, out var reason, out var error))
        {
            return Task.FromResult<IResult>(Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["reason"] = [error]
            }));
        }

        return ExecuteAsync(async () => Results.Ok(QuestionResponse.From(
            await store.ReturnForChangesAsync(
                TenantResolver.Current(httpContext),
                id,
                Subject(httpContext.User),
                reason!,
                httpContext.TraceIdentifier,
                cancellationToken))));
    }

    private static Task<IResult> ApproveQuestion(
        Guid id,
        HttpContext httpContext,
        IQuestionStore store,
        CancellationToken cancellationToken)
    {
        var ifMatch = httpContext.Request.Headers.IfMatch.ToString();
        if (string.IsNullOrWhiteSpace(ifMatch))
        {
            return Task.FromResult<IResult>(Results.Problem(
                statusCode: StatusCodes.Status428PreconditionRequired,
                title: "If-Match is required",
                type: "https://toi-no-mori.example/problems/precondition-required"));
        }

        if (!TryParseIfMatch(ifMatch, out var expectedVersion))
        {
            return Task.FromResult<IResult>(Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["ifMatch"] = ["If-Match must contain one strong, positive integer ETag."]
            }));
        }

        var idempotencyKey = httpContext.Request.Headers["Idempotency-Key"].ToString();
        if (string.IsNullOrWhiteSpace(idempotencyKey) || idempotencyKey.Length > 128)
        {
            return Task.FromResult<IResult>(Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["idempotencyKey"] = ["Idempotency-Key is required and must not exceed 128 characters."]
            }));
        }

        return ExecuteAsync(async () =>
        {
            var result = await store.ApproveAsync(
                TenantResolver.Current(httpContext),
                id,
                Subject(httpContext.User),
                expectedVersion,
                idempotencyKey,
                httpContext.TraceIdentifier,
                cancellationToken);
            httpContext.Response.Headers.ETag = QuoteVersion(result.Version);
            return Results.Ok(QuestionResponse.From(result));
        });
    }

    private static Task<IResult> WithdrawQuestion(
        Guid id,
        ReviewReasonRequest request,
        HttpContext httpContext,
        IQuestionStore store,
        CancellationToken cancellationToken)
    {
        if (!TryValidateReviewReason(request.Reason, out var reason, out var error))
        {
            return Task.FromResult<IResult>(Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["reason"] = [error]
            }));
        }

        return ExecuteAsync(async () => Results.Ok(QuestionResponse.From(
            await store.WithdrawAsync(
                TenantResolver.Current(httpContext),
                id,
                Subject(httpContext.User),
                reason!,
                httpContext.TraceIdentifier,
                cancellationToken))));
    }

    private static async Task<IResult> ExecuteAsync(Func<Task<IResult>> action)
    {
        try
        {
            return await action();
        }
        catch (DomainRuleViolationException exception)
        {
            if (exception.Code is "question.not_found" or "question.owner.forbidden")
            {
                return NotVisible();
            }

            var statusCode = exception.Code switch
            {
                "question.approve.self_forbidden" => StatusCodes.Status403Forbidden,
                "question.version.conflict" or "idempotency.key_reused" => StatusCodes.Status409Conflict,
                var code when code.EndsWith(".invalid_state", StringComparison.Ordinal) => StatusCodes.Status409Conflict,
                _ => StatusCodes.Status400BadRequest
            };

            return Results.Problem(
                statusCode: statusCode,
                title: exception.Message,
                type: $"https://toi-no-mori.example/problems/{exception.Code.Replace('.', '-')}");
        }
        catch (StoreUnavailableException)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "Persistence service unavailable",
                type: "https://toi-no-mori.example/problems/service-unavailable");
        }
    }

    private static string Subject(ClaimsPrincipal principal) =>
        principal.FindFirst("sub")?.Value
        ?? throw new InvalidOperationException("Authenticated subject claim is missing.");

    private static IResult NotVisible() => Results.Problem(
        statusCode: StatusCodes.Status404NotFound,
        title: "Resource is not visible or does not exist.",
        type: "https://toi-no-mori.example/problems/resource-not-visible-or-missing");

    private static string QuoteVersion(int version) => $"\"{version}\"";

    private static bool TryParseIfMatch(string value, out int version)
    {
        var normalized = value.Trim();
        if (normalized.Length < 3
            || normalized[0] != '"'
            || normalized[^1] != '"'
            || normalized.Contains(',', StringComparison.Ordinal))
        {
            version = 0;
            return false;
        }

        return int.TryParse(
            normalized[1..^1],
            NumberStyles.None,
            CultureInfo.InvariantCulture,
            out version) && version > 0;
    }

    private static bool TryParseStatus(string? value, out QuestionStatus? status)
    {
        status = null;
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        var normalized = value.Trim().Replace("_", string.Empty, StringComparison.Ordinal);
        if (!Enum.TryParse<QuestionStatus>(normalized, ignoreCase: true, out var parsed))
        {
            return false;
        }

        status = parsed;
        return true;
    }

    private static bool TryValidateReviewReason(
        string? value,
        out string? reason,
        out string error)
    {
        reason = value?.Trim();
        if (string.IsNullOrWhiteSpace(reason))
        {
            error = "Reason is required.";
            return false;
        }

        if (reason.Length > 1000)
        {
            error = "Reason must not exceed 1000 characters.";
            return false;
        }

        error = string.Empty;
        return true;
    }
}
