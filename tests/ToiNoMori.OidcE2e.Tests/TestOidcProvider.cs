using System.Collections.Concurrent;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace ToiNoMori.OidcE2e.Tests;

internal enum TestIdentityProfile
{
    EditorWithMfa,
    ReviewerWithMfa,
    EditorReviewerWithMfa,
    UnmappedOrganizationEditorWithMfa,
    EditorWithoutMfa,
    InvalidSignature,
    StaleAuthentication
}

internal sealed class TestOidcProvider : IAsyncDisposable
{
    public const string ClientId = "toi-no-mori-e2e";
    public const string ClientSecret = "e2e-client-secret-not-for-production";
    public const string AccessTokenSentinel = "server-only-e2e-access-token";

    private const string KeyId = "toi-no-mori-e2e-signing-key";
    private static readonly string[] ResponseTypes = ["code"];
    private static readonly string[] ResponseModes = ["query"];
    private static readonly string[] SubjectTypes = ["public"];
    private static readonly string[] SigningAlgorithms = ["RS256"];
    private static readonly string[] ClientAuthenticationMethods = ["client_secret_post"];
    private static readonly string[] CodeChallengeMethods = ["S256"];
    private static readonly string[] Scopes = ["openid", "profile"];
    private static readonly string[] Claims =
        ["sub", "name", "roles", "amr", "nonce", "auth_time", "external_organization_id"];
    private readonly ConcurrentDictionary<string, AuthorizationCodeRecord> _codes = new(StringComparer.Ordinal);
    private readonly RSA _signingRsa = RSA.Create(2048);
    private readonly RSA _rogueRsa = RSA.Create(2048);
    private TestIdentityProfile _profile;
    private readonly WebApplication _app;

    private TestOidcProvider(TestIdentityProfile profile, WebApplication app)
    {
        _profile = profile;
        _app = app;
    }

    public string Issuer { get; private set; } = string.Empty;
    public int AuthorizationRequestCount { get; private set; }
    public int TokenRequestCount { get; private set; }
    public int EndSessionRequestCount { get; private set; }
    public bool PkceVerified { get; private set; }
    public bool ClientAuthenticationVerified { get; private set; }
    public bool NonceReturned { get; private set; }

    public void SelectIdentity(TestIdentityProfile profile) => _profile = profile;

    public static async Task<TestOidcProvider> StartAsync(
        TestHttpsCertificate https,
        TestIdentityProfile profile)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ApplicationName = typeof(TestOidcProvider).Assembly.FullName,
            EnvironmentName = "Testing"
        });
        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(kestrel =>
            kestrel.Listen(IPAddress.Loopback, 0, listen => listen.UseHttps(https.Certificate)));

        var app = builder.Build();
        var provider = new TestOidcProvider(profile, app);
        provider.MapEndpoints();
        await app.StartAsync();
        provider.Issuer = app.Services
            .GetRequiredService<IServer>()
            .Features
            .Get<IServerAddressesFeature>()
            ?.Addresses
            .SingleOrDefault()
            ?? throw new InvalidOperationException("The test identity provider did not publish an address.");
        return provider;
    }

    private void MapEndpoints()
    {
        _app.MapGet("/.well-known/openid-configuration", () => Results.Json(new
        {
            issuer = Issuer,
            authorization_endpoint = $"{Issuer}/authorize",
            token_endpoint = $"{Issuer}/token",
            jwks_uri = $"{Issuer}/jwks",
            end_session_endpoint = $"{Issuer}/endsession",
            response_types_supported = ResponseTypes,
            response_modes_supported = ResponseModes,
            subject_types_supported = SubjectTypes,
            id_token_signing_alg_values_supported = SigningAlgorithms,
            token_endpoint_auth_methods_supported = ClientAuthenticationMethods,
            code_challenge_methods_supported = CodeChallengeMethods,
            scopes_supported = Scopes,
            claims_supported = Claims
        }));

        _app.MapGet("/jwks", () =>
        {
            var parameters = _signingRsa.ExportParameters(false);
            return Results.Json(new
            {
                keys = new[]
                {
                    new
                    {
                        kty = "RSA",
                        use = "sig",
                        kid = KeyId,
                        alg = "RS256",
                        n = Base64UrlEncoder.Encode(parameters.Modulus),
                        e = Base64UrlEncoder.Encode(parameters.Exponent)
                    }
                }
            });
        });

        _app.MapGet("/authorize", (HttpRequest request) => Authorize(request));
        _app.MapPost("/token", (HttpRequest request) => RedeemCodeAsync(request));
        _app.MapGet("/endsession", (HttpRequest request) => EndSession(request));
    }

    private IResult Authorize(HttpRequest request)
    {
        AuthorizationRequestCount++;
        var clientId = request.Query["client_id"].ToString();
        var redirectUri = request.Query["redirect_uri"].ToString();
        var responseType = request.Query["response_type"].ToString();
        var state = request.Query["state"].ToString();
        var nonce = request.Query["nonce"].ToString();
        var challenge = request.Query["code_challenge"].ToString();
        var challengeMethod = request.Query["code_challenge_method"].ToString();
        var maxAge = request.Query["max_age"].ToString();

        if (!string.Equals(clientId, ClientId, StringComparison.Ordinal)
            || !string.Equals(responseType, "code", StringComparison.Ordinal)
            || !Uri.TryCreate(redirectUri, UriKind.Absolute, out var callback)
            || !string.Equals(callback.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(state)
            || string.IsNullOrWhiteSpace(nonce)
            || string.IsNullOrWhiteSpace(challenge)
            || !string.Equals(challengeMethod, "S256", StringComparison.Ordinal)
            || !string.Equals(maxAge, "900", StringComparison.Ordinal))
        {
            return Results.BadRequest(new { error = "invalid_request" });
        }

        var code = Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(32));
        _codes[code] = new AuthorizationCodeRecord(redirectUri, challenge, nonce, _profile);
        var callbackParameters = new Dictionary<string, string?>
        {
            ["code"] = code,
            ["state"] = state
        };
        return Results.Redirect(QueryHelpers.AddQueryString(redirectUri, callbackParameters));
    }

    private async Task<IResult> RedeemCodeAsync(HttpRequest request)
    {
        TokenRequestCount++;
        if (!request.HasFormContentType)
        {
            return OAuthError("invalid_request");
        }

        var form = await request.ReadFormAsync(request.HttpContext.RequestAborted);
        var clientId = form["client_id"].ToString();
        var clientSecret = form["client_secret"].ToString();
        if (!SecretEquals(ClientId, clientId) || !SecretEquals(ClientSecret, clientSecret))
        {
            return OAuthError("invalid_client");
        }

        ClientAuthenticationVerified = true;
        var code = form["code"].ToString();
        var verifier = form["code_verifier"].ToString();
        var redirectUri = form["redirect_uri"].ToString();
        if (!string.Equals(form["grant_type"], "authorization_code", StringComparison.Ordinal)
            || !_codes.TryGetValue(code, out var authorizationCode)
            || !authorizationCode.TryRedeem()
            || !string.Equals(authorizationCode.RedirectUri, redirectUri, StringComparison.Ordinal)
            || !VerifyPkce(authorizationCode.Challenge, verifier))
        {
            return OAuthError("invalid_grant");
        }

        PkceVerified = true;
        var idToken = CreateIdToken(authorizationCode.Nonce, authorizationCode.Profile);
        return Results.Json(new
        {
            token_type = "Bearer",
            expires_in = 300,
            access_token = AccessTokenSentinel,
            id_token = idToken,
            scope = "openid profile"
        });
    }

    private string CreateIdToken(string nonce, TestIdentityProfile profile)
    {
        NonceReturned = !string.IsNullOrWhiteSpace(nonce);
        var now = DateTimeOffset.UtcNow;
        var authenticationTime = profile == TestIdentityProfile.StaleAuthentication
            ? now.AddMinutes(-30)
            : now;
        var identity = profile switch
        {
            TestIdentityProfile.ReviewerWithMfa => (
                Subject: "reviewer-e2e",
                Name: "E2E Reviewer",
                Roles: new[] { "Reviewer" },
                ExternalOrganizationId: "org-mvs01"),
            TestIdentityProfile.EditorReviewerWithMfa => (
                Subject: "dual-role-owner-e2e",
                Name: "E2E Dual Role Owner",
                Roles: new[] { "Editor", "Reviewer" },
                ExternalOrganizationId: "org-mvs01"),
            TestIdentityProfile.UnmappedOrganizationEditorWithMfa => (
                Subject: "unmapped-editor-e2e",
                Name: "E2E Unmapped Editor",
                Roles: new[] { "Editor" },
                ExternalOrganizationId: "org-unmapped"),
            _ => (
                Subject: "editor-e2e",
                Name: "E2E Editor",
                Roles: new[] { "Editor" },
                ExternalOrganizationId: "org-mvs01")
        };
        var claims = new Dictionary<string, object>
        {
            ["sub"] = identity.Subject,
            ["name"] = identity.Name,
            ["roles"] = identity.Roles,
            ["external_organization_id"] = identity.ExternalOrganizationId,
            ["amr"] = profile == TestIdentityProfile.EditorWithoutMfa
                ? new[] { "pwd" }
                : new[] { "pwd", "mfa" },
            ["nonce"] = nonce,
            ["auth_time"] = authenticationTime.ToUnixTimeSeconds()
        };
        var signingRsa = profile == TestIdentityProfile.InvalidSignature
            ? _rogueRsa
            : _signingRsa;
        var signingKey = new RsaSecurityKey(signingRsa) { KeyId = KeyId };
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = Issuer,
            Audience = ClientId,
            Claims = claims,
            IssuedAt = now.UtcDateTime,
            NotBefore = now.AddSeconds(-5).UtcDateTime,
            Expires = now.AddMinutes(5).UtcDateTime,
            SigningCredentials = new SigningCredentials(signingKey, SecurityAlgorithms.RsaSha256)
        };
        return new JsonWebTokenHandler().CreateToken(descriptor);
    }

    private IResult EndSession(HttpRequest request)
    {
        EndSessionRequestCount++;
        var redirectUri = request.Query["post_logout_redirect_uri"].ToString();
        var state = request.Query["state"].ToString();
        if (!Uri.TryCreate(redirectUri, UriKind.Absolute, out var callback)
            || !string.Equals(callback.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(state))
        {
            return Results.BadRequest(new { error = "invalid_request" });
        }

        return Results.Redirect(QueryHelpers.AddQueryString(redirectUri, "state", state));
    }

    private static IResult OAuthError(string error) =>
        Results.Json(new { error }, statusCode: StatusCodes.Status400BadRequest);

    private static bool VerifyPkce(string challenge, string verifier)
    {
        var calculated = Base64UrlEncoder.Encode(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));
        return SecretEquals(challenge, calculated);
    }

    private static bool SecretEquals(string expected, string supplied)
    {
        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        var suppliedBytes = Encoding.UTF8.GetBytes(supplied);
        return expectedBytes.Length == suppliedBytes.Length
            && CryptographicOperations.FixedTimeEquals(expectedBytes, suppliedBytes);
    }

    public async ValueTask DisposeAsync()
    {
        await _app.StopAsync();
        await _app.DisposeAsync();
        _signingRsa.Dispose();
        _rogueRsa.Dispose();
    }

    private sealed class AuthorizationCodeRecord(
        string redirectUri,
        string challenge,
        string nonce,
        TestIdentityProfile profile)
    {
        private int _redeemed;

        public string RedirectUri { get; } = redirectUri;
        public string Challenge { get; } = challenge;
        public string Nonce { get; } = nonce;
        public TestIdentityProfile Profile { get; } = profile;

        public bool TryRedeem() => Interlocked.Exchange(ref _redeemed, 1) == 0;
    }
}
