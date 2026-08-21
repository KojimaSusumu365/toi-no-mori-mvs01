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
        string migrationConnectionString,
        string? platformAuditWriterConnectionString = null,
        string? platformAuditReaderConnectionString = null)
    {
        platformAuditWriterConnectionString ??= Environment.GetEnvironmentVariable(
            "MVS01_TEST_POSTGRES_PLATFORM_AUDIT_WRITER_CONNECTION")
            ?? throw new InvalidOperationException(
                "MVS01_TEST_POSTGRES_PLATFORM_AUDIT_WRITER_CONNECTION is required.");
        platformAuditReaderConnectionString ??= Environment.GetEnvironmentVariable(
            "MVS01_TEST_POSTGRES_PLATFORM_AUDIT_READER_CONNECTION")
            ?? throw new InvalidOperationException(
                "MVS01_TEST_POSTGRES_PLATFORM_AUDIT_READER_CONNECTION is required.");

        var applicationSearchPath = new Npgsql.NpgsqlConnectionStringBuilder(
            applicationConnectionString).SearchPath;
        if (!string.IsNullOrWhiteSpace(applicationSearchPath))
        {
            platformAuditWriterConnectionString = new Npgsql.NpgsqlConnectionStringBuilder(
                platformAuditWriterConnectionString)
            {
                SearchPath = applicationSearchPath,
                Pooling = false
            }.ConnectionString;
            platformAuditReaderConnectionString = new Npgsql.NpgsqlConnectionStringBuilder(
                platformAuditReaderConnectionString)
            {
                SearchPath = applicationSearchPath,
                Pooling = false
            }.ConnectionString;
        }

        var apiContentRoot = Path.GetFullPath(
            Path.Combine(AppContext.BaseDirectory, "../../../../../src/ToiNoMori.Api"));
        var options = new WebApplicationOptions
        {
            Args =
            [
                "Persistence:Provider=PostgreSql",
                $"ConnectionStrings:PostgreSql={applicationConnectionString}",
                $"ConnectionStrings:PostgreSqlMigrator={migrationConnectionString}",
                $"ConnectionStrings:PostgreSqlPlatformAuditWriter={platformAuditWriterConnectionString}",
                $"ConnectionStrings:PostgreSqlPlatformAuditReader={platformAuditReaderConnectionString}",
                "Audit:PartitionHashKey=MDEyMzQ1Njc4OWFiY2RlZjAxMjM0NTY3ODlhYmNkZWY=",
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
