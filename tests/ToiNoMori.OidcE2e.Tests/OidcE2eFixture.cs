using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using ToiNoMori.Api;

namespace ToiNoMori.OidcE2e.Tests;

internal sealed class OidcE2eFixture : IAsyncDisposable
{
    private readonly TestHttpsCertificate _https;
    private readonly TestOidcProvider _identityProvider;
    private readonly WebApplication _application;
    private readonly HttpClientHandler _browserHandler;
    private readonly HttpClientHandler _backchannelHandler;

    private OidcE2eFixture(
        TestHttpsCertificate https,
        TestOidcProvider identityProvider,
        WebApplication application,
        HttpClientHandler browserHandler,
        HttpClient browser,
        HttpClientHandler backchannelHandler,
        Uri applicationBaseAddress)
    {
        _https = https;
        _identityProvider = identityProvider;
        _application = application;
        _browserHandler = browserHandler;
        Browser = browser;
        _backchannelHandler = backchannelHandler;
        ApplicationBaseAddress = applicationBaseAddress;
    }

    public HttpClient Browser { get; }
    public Uri ApplicationBaseAddress { get; }
    public TestOidcProvider IdentityProvider => _identityProvider;
    public InMemoryQuestionStore Store =>
        _application.Services.GetRequiredService<InMemoryQuestionStore>();

    public IEnumerable<Cookie> Cookies => _browserHandler.CookieContainer.GetAllCookies().Cast<Cookie>();

    public Cookie? SessionCookie => Cookies.SingleOrDefault(cookie =>
        string.Equals(cookie.Name, "__Host-toi-no-mori-session", StringComparison.Ordinal));

    public static async Task<OidcE2eFixture> StartAsync(TestIdentityProfile profile)
    {
        var https = TestHttpsCertificate.Create();
        TestOidcProvider? identityProvider = null;
        WebApplication? application = null;
        HttpClientHandler? backchannelHandler = null;
        HttpClientHandler? browserHandler = null;
        HttpClient? browser = null;
        try
        {
            identityProvider = await TestOidcProvider.StartAsync(https, profile);
            backchannelHandler = CertificatePinnedHandler(https);

            var projectRoot = Directory.GetCurrentDirectory();
            var options = new WebApplicationOptions
            {
                Args =
                [
                    "Authentication:Mode=Oidc",
                    $"Authentication:Oidc:Authority={identityProvider.Issuer}",
                    $"Authentication:Oidc:ClientId={TestOidcProvider.ClientId}",
                    $"Authentication:Oidc:ClientSecret={TestOidcProvider.ClientSecret}",
                    "Authentication:Oidc:RoleClaimType=roles",
                    "Authentication:Oidc:MfaClaimType=amr",
                    "Authentication:Oidc:MfaClaimValue=mfa",
                    $"Tenancy:Organizations:mvs01:Issuer={identityProvider.Issuer}",
                    "Tenancy:Organizations:mvs01:ExternalOrganizationId=org-mvs01",
                    $"Tenancy:Organizations:mvs01:InternalTenantId={ToiNoMori.Domain.TenantIds.Mvs01:D}",
                    "Logging:LogLevel:Default=Error",
                    "Logging:LogLevel:Microsoft.AspNetCore.Authentication.OpenIdConnect=None",
                    "Logging:LogLevel:Microsoft.AspNetCore.DataProtection=Error"
                ],
                EnvironmentName = "Testing",
                ApplicationName = typeof(AppHost).Assembly.FullName,
                ContentRootPath = Path.Combine(projectRoot, "src", "ToiNoMori.Api")
            };

            var configuredBackchannelHandler = backchannelHandler;
            application = AppHost.Build(options, builder =>
            {
                builder.WebHost.ConfigureKestrel(kestrel =>
                    kestrel.Listen(IPAddress.Loopback, 0, listen => listen.UseHttps(https.Certificate)));
                builder.Services.Configure<OpenIdConnectOptions>(
                    BffAuthentication.OidcScheme,
                    oidc =>
                    {
                        oidc.BackchannelHttpHandler = configuredBackchannelHandler;
                        oidc.BackchannelTimeout = TimeSpan.FromSeconds(10);
                    });
            });
            await application.StartAsync();
            var publishedAddress = application.Services
                .GetRequiredService<IServer>()
                .Features
                .Get<IServerAddressesFeature>()
                ?.Addresses
                .SingleOrDefault()
                ?? throw new InvalidOperationException("The application did not publish an HTTPS address.");
            var applicationBaseAddress = new UriBuilder(publishedAddress)
            {
                Host = "localhost"
            }.Uri;

            browserHandler = CertificatePinnedHandler(https);
            browserHandler.AllowAutoRedirect = true;
            browserHandler.CookieContainer = new CookieContainer();
            browserHandler.UseCookies = true;
            browser = new HttpClient(browserHandler)
            {
                BaseAddress = applicationBaseAddress,
                Timeout = TimeSpan.FromSeconds(20)
            };

            return new(
                https,
                identityProvider,
                application,
                browserHandler,
                browser,
                backchannelHandler,
                applicationBaseAddress);
        }
        catch
        {
            browser?.Dispose();
            browserHandler?.Dispose();
            backchannelHandler?.Dispose();
            if (application is not null)
            {
                await application.DisposeAsync();
            }
            if (identityProvider is not null)
            {
                await identityProvider.DisposeAsync();
            }
            https.Dispose();
            throw;
        }
    }

    public Task<HttpResponseMessage> LoginAsync() =>
        Browser.GetAsync("/bff/login?returnUrl=/app/");

    public Task<HttpResponseMessage> LoginAsync(HttpClient browser, TestIdentityProfile profile)
    {
        _identityProvider.SelectIdentity(profile);
        return browser.GetAsync("/bff/login?returnUrl=/app/");
    }

    public Task<HttpResponseMessage> GetSessionAsync() =>
        Browser.GetAsync("/bff/session");

    public Task<string> ReadCsrfTokenAsync() => ReadCsrfTokenAsync(Browser);

    public static async Task<string> ReadCsrfTokenAsync(HttpClient browser)
    {
        using var response = await browser.GetAsync("/bff/session");
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        return payload.GetProperty("csrfToken").GetString()
            ?? throw new InvalidOperationException("The BFF session did not return a CSRF token.");
    }

    public HttpClient CreateBrowser()
    {
        var handler = CertificatePinnedHandler(_https);
        handler.AllowAutoRedirect = true;
        handler.CookieContainer = new CookieContainer();
        handler.UseCookies = true;
        return new HttpClient(handler)
        {
            BaseAddress = ApplicationBaseAddress,
            Timeout = TimeSpan.FromSeconds(20)
        };
    }

    private static HttpClientHandler CertificatePinnedHandler(TestHttpsCertificate https) => new()
    {
        UseProxy = false,
        ServerCertificateCustomValidationCallback = (_, certificate, _, _) =>
            https.Matches(certificate)
    };

    public async ValueTask DisposeAsync()
    {
        Browser.Dispose();
        _browserHandler.Dispose();
        await _application.StopAsync();
        await _application.DisposeAsync();
        _backchannelHandler.Dispose();
        await _identityProvider.DisposeAsync();
        _https.Dispose();
    }
}
