using System.Security.Claims;
using System.Security.Cryptography;
using System.Globalization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace ToiNoMori.Api;

public static class BffAuthentication
{
    public const string CookieScheme = "BffCookie";
    public const string OidcScheme = "Oidc";

    public static bool Configure(WebApplicationBuilder builder)
    {
        var mode = builder.Configuration["Authentication:Mode"] ?? "Disabled";
        if (string.Equals(mode, "Disabled", StringComparison.OrdinalIgnoreCase))
        {
            if (builder.Environment.IsProduction())
            {
                throw new InvalidOperationException(
                    "Production requires Authentication:Mode=Oidc. Administrative access remains disabled.");
            }

            builder.Services
                .AddAuthentication(authentication =>
                {
                    authentication.DefaultAuthenticateScheme = "Disabled";
                    authentication.DefaultChallengeScheme = "Disabled";
                    authentication.DefaultForbidScheme = "Disabled";
                })
                .AddScheme<AuthenticationSchemeOptions, DisabledAuthenticationHandler>(
                    "Disabled",
                    _ => { });
            return false;
        }

        if (!string.Equals(mode, "Oidc", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Unsupported authentication mode: {mode}");
        }

        var authority = Required(builder, "Authentication:Oidc:Authority");
        var clientId = Required(builder, "Authentication:Oidc:ClientId");
        var clientSecret = Required(builder, "Authentication:Oidc:ClientSecret");
        if (!Uri.TryCreate(authority, UriKind.Absolute, out var authorityUri)
            || !string.Equals(authorityUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Authentication:Oidc:Authority must be an absolute HTTPS URI.");
        }

        var nameClaimType = builder.Configuration["Authentication:Oidc:NameClaimType"] ?? "name";
        var roleClaimType = builder.Configuration["Authentication:Oidc:RoleClaimType"] ?? "role";
        var maximumAuthenticationAgeMinutes = builder.Configuration.GetValue(
            "Authentication:Oidc:MaxAuthenticationAgeMinutes",
            15);
        if (maximumAuthenticationAgeMinutes is < 1 or > 60)
        {
            throw new InvalidOperationException(
                "Authentication:Oidc:MaxAuthenticationAgeMinutes must be between 1 and 60.");
        }
        var maximumAuthenticationAge = TimeSpan.FromMinutes(maximumAuthenticationAgeMinutes);

        builder.Services
            .AddAuthentication(authentication =>
            {
                authentication.DefaultAuthenticateScheme = CookieScheme;
                authentication.DefaultSignInScheme = CookieScheme;
                authentication.DefaultChallengeScheme = CookieScheme;
                authentication.DefaultForbidScheme = CookieScheme;
                authentication.DefaultSignOutScheme = OidcScheme;
            })
            .AddCookie(CookieScheme, cookie =>
            {
                cookie.Cookie.Name = "__Host-toi-no-mori-session";
                cookie.Cookie.HttpOnly = true;
                cookie.Cookie.IsEssential = true;
                cookie.Cookie.Path = "/";
                cookie.Cookie.SameSite = SameSiteMode.Lax;
                cookie.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                cookie.ExpireTimeSpan = TimeSpan.FromMinutes(20);
                cookie.SlidingExpiration = false;
                cookie.Events.OnRedirectToLogin = context =>
                {
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    return Task.CompletedTask;
                };
                cookie.Events.OnRedirectToAccessDenied = context =>
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    return Task.CompletedTask;
                };
            })
            .AddOpenIdConnect(OidcScheme, oidc =>
            {
                oidc.Authority = authority;
                oidc.ClientId = clientId;
                oidc.ClientSecret = clientSecret;
                oidc.SignInScheme = CookieScheme;
                oidc.ResponseType = OpenIdConnectResponseType.Code;
                oidc.UsePkce = true;
                oidc.SaveTokens = false;
                oidc.GetClaimsFromUserInfoEndpoint = false;
                oidc.MapInboundClaims = false;
                oidc.RequireHttpsMetadata = true;
                oidc.UseTokenLifetime = false;
                oidc.MaxAge = maximumAuthenticationAge;
                oidc.RemoteAuthenticationTimeout = TimeSpan.FromMinutes(5);
                oidc.CallbackPath = "/signin-oidc";
                oidc.SignedOutCallbackPath = "/signout-callback-oidc";
                oidc.Scope.Clear();
                oidc.Scope.Add("openid");
                oidc.Scope.Add("profile");
                oidc.TokenValidationParameters.NameClaimType = nameClaimType;
                oidc.TokenValidationParameters.RoleClaimType = roleClaimType;
                oidc.TokenValidationParameters.ValidateIssuer = true;
                oidc.TokenValidationParameters.ValidateAudience = true;
                oidc.TokenValidationParameters.ValidAudience = clientId;
                oidc.TokenValidationParameters.ValidateLifetime = true;
                oidc.TokenValidationParameters.RequireExpirationTime = true;
                oidc.TokenValidationParameters.RequireSignedTokens = true;
                oidc.TokenValidationParameters.ClockSkew = TimeSpan.FromMinutes(1);

                ConfigureProtocolCookie(oidc.CorrelationCookie, "__Host-toi-no-mori-oidc-correlation");
                ConfigureProtocolCookie(oidc.NonceCookie, "__Host-toi-no-mori-oidc-nonce");

                oidc.Events.OnTokenValidated = context =>
                {
                    if (context.Principal?.Identity is not ClaimsIdentity identity
                        || string.IsNullOrWhiteSpace(context.Principal.FindFirst("sub")?.Value))
                    {
                        context.Fail("The identity provider did not supply a subject claim.");
                        return Task.CompletedTask;
                    }

                    var authenticationTimeValue = context.Principal.FindFirst("auth_time")?.Value;
                    if (!long.TryParse(
                            authenticationTimeValue,
                            NumberStyles.None,
                            CultureInfo.InvariantCulture,
                            out var authenticationTimeSeconds))
                    {
                        context.Fail("The identity provider did not supply a valid authentication time.");
                        return Task.CompletedTask;
                    }

                    DateTimeOffset authenticationTime;
                    try
                    {
                        authenticationTime = DateTimeOffset.FromUnixTimeSeconds(authenticationTimeSeconds);
                    }
                    catch (ArgumentOutOfRangeException)
                    {
                        context.Fail("The identity provider supplied an out-of-range authentication time.");
                        return Task.CompletedTask;
                    }

                    var now = context.HttpContext.RequestServices
                        .GetRequiredService<TimeProvider>()
                        .GetUtcNow();
                    var allowedClockSkew = TimeSpan.FromMinutes(1);
                    if (authenticationTime > now.Add(allowedClockSkew)
                        || now - authenticationTime > maximumAuthenticationAge.Add(allowedClockSkew))
                    {
                        context.Fail("The identity provider authentication is not recent enough.");
                        return Task.CompletedTask;
                    }

                    Guid internalTenantId;
                    try
                    {
                        var tenantResolver = context.HttpContext.RequestServices
                            .GetRequiredService<TenantResolver>();
                        internalTenantId = tenantResolver.ResolveExternal(
                            context.SecurityToken.Issuer,
                            context.Principal
                                .FindAll(TenantResolver.ExternalOrganizationClaimType)
                                .Select(claim => claim.Value));
                    }
                    catch (TenantResolutionException)
                    {
                        context.Fail("OIDC tenant mapping failed.");
                        return Task.CompletedTask;
                    }

                    foreach (var claim in identity.Claims.Where(claim =>
                        claim.Type == TenantResolver.ExternalOrganizationClaimType
                        || claim.Type == TenantResolver.VerifiedIssuerClaimType
                        || claim.Type == TenantResolver.InternalTenantClaimType).ToArray())
                    {
                        identity.RemoveClaim(claim);
                    }
                    identity.AddClaim(new Claim(
                        TenantResolver.InternalTenantClaimType,
                        internalTenantId.ToString("D")));

                    if (!identity.HasClaim(claim => claim.Type == "csrf"))
                    {
                        identity.AddClaim(new Claim(
                            "csrf",
                            WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32))));
                    }

                    return Task.CompletedTask;
                };
                oidc.Events.OnRemoteFailure = context =>
                {
                    context.HandleResponse();
                    context.Response.Redirect("/app/?authentication=failed");
                    return Task.CompletedTask;
                };
            });

        return true;
    }

    private static void ConfigureProtocolCookie(CookieBuilder cookie, string name)
    {
        cookie.Name = name;
        cookie.HttpOnly = true;
        cookie.IsEssential = true;
        cookie.Path = "/";
        cookie.SameSite = SameSiteMode.None;
        cookie.SecurePolicy = CookieSecurePolicy.Always;
    }

    private static string Required(WebApplicationBuilder builder, string key) =>
        !string.IsNullOrWhiteSpace(builder.Configuration[key])
            ? builder.Configuration[key]!
            : throw new InvalidOperationException($"{key} is required when Authentication:Mode=Oidc.");
}
