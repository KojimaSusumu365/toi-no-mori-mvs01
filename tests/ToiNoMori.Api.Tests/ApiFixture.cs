using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using ToiNoMori.Api;

namespace ToiNoMori.Api.Tests;

public sealed class ApiFixture : IAsyncDisposable
{
    private readonly WebApplication _app;
    private readonly Uri _baseAddress;

    private ApiFixture(WebApplication app, Uri baseAddress)
    {
        _app = app;
        _baseAddress = baseAddress;
    }

    public InMemoryQuestionStore Store => _app.Services.GetRequiredService<InMemoryQuestionStore>();

    public Guid PublicReadTenantId =>
        _app.Services.GetRequiredService<PublicReadTenantContext>().TenantId;

    public SecurityAuditMetrics AuditMetrics =>
        _app.Services.GetRequiredService<SecurityAuditMetrics>();

    public static async Task<ApiFixture> StartAsync(
        string environmentName = "Testing",
        string[]? configuration = null,
        Action<IServiceCollection>? configureServices = null)
    {
        var arguments = new List<string>
        {
            "Logging:LogLevel:Default=Warning",
            "Logging:LogLevel:Microsoft.AspNetCore.DataProtection=Error"
        };
        if (configuration is not null)
        {
            arguments.AddRange(configuration);
        }

        var options = new WebApplicationOptions
        {
            Args = [.. arguments],
            EnvironmentName = environmentName,
            ApplicationName = typeof(AppHost).Assembly.FullName,
            ContentRootPath = Path.Combine(Directory.GetCurrentDirectory(), "src", "ToiNoMori.Api")
        };

        var app = AppHost.Build(
            options,
            environmentName == "Testing"
                ? builder =>
                {
                    builder.Services
                        .AddAuthentication(authentication =>
                        {
                            authentication.DefaultAuthenticateScheme = "TestHeaders";
                            authentication.DefaultChallengeScheme = "TestHeaders";
                            authentication.DefaultForbidScheme = "TestHeaders";
                        })
                        .AddScheme<AuthenticationSchemeOptions, TestHeaderAuthenticationHandler>(
                            "TestHeaders",
                            _ => { });
                    configureServices?.Invoke(builder.Services);
                }
                : null);
        app.Urls.Add("http://127.0.0.1:0");
        await app.StartAsync();

        var addresses = app.Services
            .GetRequiredService<IServer>()
            .Features
            .Get<IServerAddressesFeature>()
            ?.Addresses;
        var address = addresses?.SingleOrDefault()
            ?? throw new InvalidOperationException("Kestrel did not publish a test address.");

        return new(app, new(address));
    }

    public HttpClient AnonymousClient() => new()
    {
        BaseAddress = _baseAddress
    };

    public HttpClient AuthenticatedClient(
        string subject,
        string role,
        bool includeCsrfHeader = true,
        bool includeMfa = true,
        string? externalOrganizationId = "org-mvs01",
        string? verifiedIssuer = "https://test-identity.example")
    {
        const string csrf = "test-csrf-token";
        var client = AnonymousClient();
        client.DefaultRequestHeaders.Add("X-Test-Subject", subject);
        client.DefaultRequestHeaders.Add("X-Test-Role", role);
        client.DefaultRequestHeaders.Add("X-Test-Csrf", csrf);
        if (includeMfa)
        {
            client.DefaultRequestHeaders.Add("X-Test-Amr", "mfa");
        }
        if (!string.IsNullOrWhiteSpace(externalOrganizationId))
        {
            client.DefaultRequestHeaders.Add(
                "X-Test-External-Organization",
                externalOrganizationId);
        }
        if (!string.IsNullOrWhiteSpace(verifiedIssuer))
        {
            client.DefaultRequestHeaders.Add("X-Test-Verified-Issuer", verifiedIssuer);
        }
        if (includeCsrfHeader)
        {
            client.DefaultRequestHeaders.Add("X-CSRF-Token", csrf);
        }

        return client;
    }

    public async ValueTask DisposeAsync()
    {
        await _app.StopAsync();
        await _app.DisposeAsync();
    }
}
