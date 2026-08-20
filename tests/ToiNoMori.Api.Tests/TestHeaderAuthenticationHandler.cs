using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ToiNoMori.Api.Tests;

/// <summary>
/// API仕様テスト専用。Production assemblyには含まれない。
/// </summary>
public sealed class TestHeaderAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var subject = Request.Headers["X-Test-Subject"].ToString();
        var role = Request.Headers["X-Test-Role"].ToString();
        var csrf = Request.Headers["X-Test-Csrf"].ToString();
        var authenticationMethods = Request.Headers["X-Test-Amr"].ToString();
        var externalOrganizationId = Request.Headers["X-Test-External-Organization"].ToString();
        var verifiedIssuer = Request.Headers["X-Test-Verified-Issuer"].ToString();
        if (string.IsNullOrWhiteSpace(subject) || string.IsNullOrWhiteSpace(role))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var claims = new List<Claim>
        {
            new("sub", subject),
            new(ClaimTypes.NameIdentifier, subject),
            new(ClaimTypes.Name, subject)
        };

        claims.AddRange(role.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(value => new Claim(ClaimTypes.Role, value)));
        claims.AddRange(authenticationMethods
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(value => new Claim("amr", value)));

        if (!string.IsNullOrWhiteSpace(verifiedIssuer))
        {
            claims.Add(new(TenantResolver.VerifiedIssuerClaimType, verifiedIssuer));
        }

        if (!string.IsNullOrWhiteSpace(externalOrganizationId))
        {
            claims.AddRange(externalOrganizationId
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(value => new Claim("external_organization_id", value)));
        }

        if (!string.IsNullOrWhiteSpace(csrf))
        {
            claims.Add(new("csrf", csrf));
        }

        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, Scheme.Name));
        return Task.FromResult(AuthenticateResult.Success(new(principal, Scheme.Name)));
    }
}
