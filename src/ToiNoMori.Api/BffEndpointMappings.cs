using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;

namespace ToiNoMori.Api;

public static class BffEndpointMappings
{
    public static void MapBffEndpoints(this WebApplication app, bool oidcEnabled)
    {
        var bff = app.MapGroup("/bff");

        bff.MapGet("/config", () => Results.Ok(new { signInEnabled = oidcEnabled }));

        bff.MapGet("/login", (string? returnUrl) =>
        {
            if (!oidcEnabled)
            {
                return Results.Problem(
                    statusCode: StatusCodes.Status503ServiceUnavailable,
                    title: "Interactive sign-in is not configured",
                    type: "https://toi-no-mori.example/problems/sign-in-unavailable");
            }

            return Results.Challenge(
                new AuthenticationProperties { RedirectUri = NormalizeReturnUrl(returnUrl) },
                [BffAuthentication.OidcScheme]);
        });

        bff.MapGet("/session", (ClaimsPrincipal user) =>
        {
            var roleClaimTypes = user.Identities
                .Select(identity => identity.RoleClaimType)
                .Append(ClaimTypes.Role)
                .Append("role")
                .Append("roles")
                .ToHashSet(StringComparer.Ordinal);
            var roles = user.Claims
                .Where(claim => roleClaimTypes.Contains(claim.Type))
                .Select(claim => claim.Value)
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToArray();
            return Results.Ok(new BffSessionResponse(
                user.FindFirst("sub")?.Value ?? string.Empty,
                user.Identity?.Name ?? user.FindFirst("name")?.Value ?? string.Empty,
                roles,
                user.FindFirst("csrf")?.Value ?? string.Empty));
        }).RequireAuthorization("MfaAuthenticated");

        bff.MapPost("/logout", (string? returnUrl) =>
        {
            if (!oidcEnabled)
            {
                return Results.Problem(
                    statusCode: StatusCodes.Status503ServiceUnavailable,
                    title: "Interactive sign-out is not configured",
                    type: "https://toi-no-mori.example/problems/sign-out-unavailable");
            }

            return Results.SignOut(
                new AuthenticationProperties { RedirectUri = NormalizeReturnUrl(returnUrl) },
                [BffAuthentication.CookieScheme, BffAuthentication.OidcScheme]);
        })
            .RequireAuthorization("MfaAuthenticated")
            .AddEndpointFilter<RequireCsrfFilter>();
    }

    public static string NormalizeReturnUrl(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)
            || value.Any(char.IsControl)
            || value.Contains('\\')
            || !Uri.IsWellFormedUriString(value, UriKind.Relative)
            || !value.StartsWith("/app", StringComparison.Ordinal)
            || value.Length > 4 && value[4] is not ('/' or '?' or '#'))
        {
            return "/app/";
        }

        return value;
    }

    private sealed record BffSessionResponse(
        string Subject,
        string DisplayName,
        IReadOnlyList<string> Roles,
        string CsrfToken);
}
