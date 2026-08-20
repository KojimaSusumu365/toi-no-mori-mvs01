using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using ToiNoMori.Api;
using ToiNoMori.Api.Tests;

namespace ToiNoMori.PostgreSql.Tests;

public sealed class PostgreSqlApiFixture : IAsyncDisposable
{
    private readonly WebApplication _app;
    private readonly Uri _baseAddress;

    private PostgreSqlApiFixture(WebApplication app, Uri baseAddress)
    {
        _app = app;
        _baseAddress = baseAddress;
    }

    public static async Task<PostgreSqlApiFixture> StartAsync(
        string applicationConnectionString,
        string migrationConnectionString)
    {
        var apiContentRoot = Path.GetFullPath(
            Path.Combine(AppContext.BaseDirectory, "../../../../../src/ToiNoMori.Api"));
        var options = new WebApplicationOptions
        {
            Args =
            [
                "Persistence:Provider=PostgreSql",
                $"ConnectionStrings:PostgreSql={applicationConnectionString}",
                $"ConnectionStrings:PostgreSqlMigrator={migrationConnectionString}",
                "Logging:LogLevel:Default=Warning",
                "Logging:LogLevel:Microsoft.AspNetCore.DataProtection=Error"
            ],
            EnvironmentName = "Testing",
            ApplicationName = typeof(AppHost).Assembly.FullName,
            ContentRootPath = apiContentRoot
        };

        var app = AppHost.Build(
            options,
            builder => builder.Services
                .AddAuthentication(authentication =>
                {
                    authentication.DefaultAuthenticateScheme = "TestHeaders";
                    authentication.DefaultChallengeScheme = "TestHeaders";
                    authentication.DefaultForbidScheme = "TestHeaders";
                })
                .AddScheme<AuthenticationSchemeOptions, TestHeaderAuthenticationHandler>(
                    "TestHeaders",
                    _ => { }));
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
        string externalOrganizationId = "org-mvs01")
    {
        const string csrf = "test-csrf-token";
        var client = AnonymousClient();
        client.DefaultRequestHeaders.Add("X-Test-Subject", subject);
        client.DefaultRequestHeaders.Add("X-Test-Role", role);
        client.DefaultRequestHeaders.Add("X-Test-Amr", "mfa");
        client.DefaultRequestHeaders.Add("X-Test-External-Organization", externalOrganizationId);
        client.DefaultRequestHeaders.Add("X-Test-Verified-Issuer", "https://test-identity.example");
        client.DefaultRequestHeaders.Add("X-Test-Csrf", csrf);
        client.DefaultRequestHeaders.Add("X-CSRF-Token", csrf);
        return client;
    }

    public async ValueTask DisposeAsync()
    {
        await _app.StopAsync();
        await _app.DisposeAsync();
    }
}
