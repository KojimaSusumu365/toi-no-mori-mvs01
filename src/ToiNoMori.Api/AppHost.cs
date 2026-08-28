using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Encodings.Web;
using System.Security.Cryptography.X509Certificates;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.RateLimiting;
using Npgsql;
using ToiNoMori.Api.Persistence;

namespace ToiNoMori.Api;

public static class AppHost
{
    public static WebApplication Build(
        WebApplicationOptions options,
        Action<WebApplicationBuilder>? configureTestingServices = null)
    {
        var builder = WebApplication.CreateBuilder(options);

        builder.Services.AddProblemDetails();
        if (builder.Environment.IsEnvironment("Testing"))
        {
            builder.Logging.SetMinimumLevel(LogLevel.Warning);
        }

        builder.Services.ConfigureHttpJsonOptions(json =>
        {
            json.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
            json.SerializerOptions.Encoder = JavaScriptEncoder.Default;
            json.SerializerOptions.Converters.Add(
                new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseUpper));
        });

        builder.Services.AddSingleton<TimeProvider>(TimeProvider.System);
        builder.Services.AddSingleton<SecurityAuditMetrics>();
        builder.Services.AddSingleton<SecurityAuditPartitionHasher>();
        builder.Services.AddSingleton<SecurityAuditQueue>();
        builder.Services.AddHostedService<SecurityAuditWorker>();
        builder.Services.AddSingleton<TenantResolver>();
        builder.Services.AddSingleton(new PublicReadTenantContext(builder.Configuration));
        builder.Services.AddSingleton<RequireTenantFilter>();
        ConfigurePersistence(builder);
        builder.Services.AddSingleton<RequireCsrfFilter>();
        var oidcEnabled = BffAuthentication.Configure(builder);
        ConfigureDataProtection(builder);

        var mfaClaimType = builder.Configuration["Authentication:Oidc:MfaClaimType"] ?? "amr";
        var mfaClaimValue = builder.Configuration["Authentication:Oidc:MfaClaimValue"] ?? "mfa";
        if (string.IsNullOrWhiteSpace(mfaClaimType) || string.IsNullOrWhiteSpace(mfaClaimValue))
        {
            throw new InvalidOperationException(
                "Authentication:Oidc:MfaClaimType and MfaClaimValue must not be empty.");
        }

        builder.Services.AddAuthorizationBuilder()
            .AddPolicy("MfaAuthenticated", policy => policy
                .RequireAuthenticatedUser()
                .RequireClaim(mfaClaimType, mfaClaimValue))
            .AddPolicy("Editor", policy => policy
                .RequireAuthenticatedUser()
                .RequireClaim(mfaClaimType, mfaClaimValue)
                .RequireRole("Editor"))
            .AddPolicy("Staff", policy => policy
                .RequireAuthenticatedUser()
                .RequireClaim(mfaClaimType, mfaClaimValue)
                .RequireRole("Editor", "Reviewer"))
            .AddPolicy("Reviewer", policy => policy
                .RequireAuthenticatedUser()
                .RequireClaim(mfaClaimType, mfaClaimValue)
                .RequireRole("Reviewer"))
            .AddPolicy("Auditor", policy => policy
                .RequireAuthenticatedUser()
                .RequireClaim(mfaClaimType, mfaClaimValue)
                .RequireRole("Auditor"))
            .AddPolicy("PlatformAuditor", policy => policy
                .RequireAuthenticatedUser()
                .RequireClaim(mfaClaimType, mfaClaimValue)
                .RequireRole("PlatformAuditor"));

        builder.Services.AddRateLimiter(rateLimiter =>
        {
            rateLimiter.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            rateLimiter.AddPolicy("public", context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = builder.Configuration.GetValue("PublicRateLimit:PermitLimit", 100),
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0,
                        AutoReplenishment = true
                    }));
        });

        configureTestingServices?.Invoke(builder);

        var app = builder.Build();

        if (app.Environment.IsProduction())
        {
            app.UseHsts();
            app.UseHttpsRedirection();
        }

        app.UseMiddleware<CorrelationContextMiddleware>();
        app.UseMiddleware<SecurityAuditMiddleware>();

        app.Use(async (context, next) =>
        {
            context.Response.Headers["X-Content-Type-Options"] = "nosniff";
            context.Response.Headers["Referrer-Policy"] = "no-referrer";
            context.Response.Headers["X-Frame-Options"] = "DENY";
            context.Response.Headers["Cross-Origin-Opener-Policy"] = "same-origin";
            context.Response.Headers["Cross-Origin-Resource-Policy"] = "same-origin";
            context.Response.Headers["Content-Security-Policy"] =
                "default-src 'none'; base-uri 'none'; frame-ancestors 'none'; form-action 'self'; "
                + "script-src 'self'; style-src 'self'; img-src 'self' data:; connect-src 'self'; "
                + "font-src 'self'; manifest-src 'self'; object-src 'none'";
            context.Response.Headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
            if (context.Request.Path.StartsWithSegments("/api/admin")
                || context.Request.Path.StartsWithSegments("/api/ops")
                || context.Request.Path.StartsWithSegments("/api/platform")
                || context.Request.Path.StartsWithSegments("/bff")
                || context.Request.Path == "/app/"
                || context.Request.Path == "/app/index.html")
            {
                context.Response.Headers.CacheControl = "no-store";
            }

            await next(context);
        });

        app.UseDefaultFiles();
        app.UseStaticFiles();
        app.UseRateLimiter();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapGet("/", () => Results.Redirect("/app/"));
        app.MapBffEndpoints(oidcEnabled);
        app.MapMvs01Endpoints();

        return app;
    }

    private static void ConfigureDataProtection(WebApplicationBuilder builder)
    {
        var dataProtection = builder.Services
            .AddDataProtection()
            .SetApplicationName("ToiNoMori.Mvs01");
        if (!builder.Environment.IsProduction())
        {
            dataProtection.UseEphemeralDataProtectionProvider();
            return;
        }

        var keyRingPath = builder.Configuration["DataProtection:KeyRingPath"];
        var certificatePath = builder.Configuration["DataProtection:CertificatePath"];
        if (string.IsNullOrWhiteSpace(keyRingPath)
            || !Path.IsPathFullyQualified(keyRingPath)
            || !Directory.Exists(keyRingPath))
        {
            throw new InvalidOperationException(
                "Production requires an existing absolute DataProtection:KeyRingPath shared by API instances.");
        }

        if (string.IsNullOrWhiteSpace(certificatePath)
            || !Path.IsPathFullyQualified(certificatePath)
            || !File.Exists(certificatePath))
        {
            throw new InvalidOperationException(
                "Production requires an existing absolute DataProtection:CertificatePath.");
        }

        var certificate = X509CertificateLoader.LoadPkcs12FromFile(
            certificatePath,
            builder.Configuration["DataProtection:CertificatePassword"],
            X509KeyStorageFlags.EphemeralKeySet);
        if (!certificate.HasPrivateKey)
        {
            certificate.Dispose();
            throw new InvalidOperationException("The data-protection certificate must include a private key.");
        }

        builder.Services.AddSingleton(certificate);
        dataProtection
            .PersistKeysToFileSystem(new DirectoryInfo(keyRingPath))
            .ProtectKeysWithCertificate(certificate);
    }

    private static void ConfigurePersistence(WebApplicationBuilder builder)
    {
        var provider = builder.Configuration["Persistence:Provider"] ?? "InMemory";
        if (builder.Environment.IsProduction()
            && !string.Equals(provider, "PostgreSql", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Production requires Persistence:Provider=PostgreSql.");
        }

        if (string.Equals(provider, "InMemory", StringComparison.OrdinalIgnoreCase))
        {
            builder.Services.AddSingleton<InMemoryQuestionStore>();
            builder.Services.AddSingleton<IQuestionStore>(services =>
                services.GetRequiredService<InMemoryQuestionStore>());
            builder.Services.AddSingleton<InMemoryPlatformSecurityAuditStore>();
            builder.Services.AddSingleton<IAuditSink>(services =>
                services.GetRequiredService<InMemoryPlatformSecurityAuditStore>());
            builder.Services.AddSingleton<IPlatformSecurityEventReader>(services =>
                services.GetRequiredService<InMemoryPlatformSecurityAuditStore>());
        }
        else if (string.Equals(provider, "PostgreSql", StringComparison.OrdinalIgnoreCase))
        {
            var rawApplicationConnectionString = builder.Configuration.GetConnectionString("PostgreSql");
            if (string.IsNullOrWhiteSpace(rawApplicationConnectionString))
            {
                throw new InvalidOperationException("ConnectionStrings:PostgreSql is required.");
            }

            var applicationConnectionString = new NpgsqlConnectionStringBuilder(
                rawApplicationConnectionString)
            {
                ApplicationName = "ToiNoMori.Mvs01.Application",
                IncludeErrorDetail = false,
                LogParameters = false
            };
            if (builder.Environment.IsProduction()
                && applicationConnectionString.SslMode != SslMode.VerifyFull)
            {
                throw new InvalidOperationException(
                    "Production PostgreSQL connections must use SSL Mode=VerifyFull.");
            }

            var rawMigrationConnectionString = builder.Configuration
                .GetConnectionString("PostgreSqlMigrator");
            if (string.IsNullOrWhiteSpace(rawMigrationConnectionString))
            {
                throw new InvalidOperationException(
                    "ConnectionStrings:PostgreSqlMigrator is required for a separate migration credential.");
            }

            var migrationConnectionString = new NpgsqlConnectionStringBuilder(
                rawMigrationConnectionString)
            {
                ApplicationName = "ToiNoMori.Mvs01.Migration",
                IncludeErrorDetail = false,
                LogParameters = false
            };
            if (builder.Environment.IsProduction()
                && migrationConnectionString.SslMode != SslMode.VerifyFull)
            {
                throw new InvalidOperationException(
                    "Production PostgreSQL migration connections must use SSL Mode=VerifyFull.");
            }

            if (string.IsNullOrWhiteSpace(applicationConnectionString.Username)
                || string.IsNullOrWhiteSpace(migrationConnectionString.Username))
            {
                throw new InvalidOperationException(
                    "PostgreSQL application and migration connections require explicit usernames.");
            }

            if (string.Equals(
                applicationConnectionString.Username,
                migrationConnectionString.Username,
                StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "PostgreSQL application and migration connections must use different roles.");
            }

            var rawPlatformAuditWriterConnectionString = builder.Configuration
                .GetConnectionString("PostgreSqlPlatformAuditWriter");
            var rawPlatformAuditReaderConnectionString = builder.Configuration
                .GetConnectionString("PostgreSqlPlatformAuditReader");
            if (string.IsNullOrWhiteSpace(rawPlatformAuditWriterConnectionString)
                || string.IsNullOrWhiteSpace(rawPlatformAuditReaderConnectionString))
            {
                throw new InvalidOperationException(
                    "Separate PostgreSqlPlatformAuditWriter and PostgreSqlPlatformAuditReader connections are required.");
            }

            var platformAuditWriterConnectionString = new NpgsqlConnectionStringBuilder(
                rawPlatformAuditWriterConnectionString)
            {
                ApplicationName = "ToiNoMori.Mvs01.PlatformAuditWriter",
                IncludeErrorDetail = false,
                LogParameters = false
            };
            var platformAuditReaderConnectionString = new NpgsqlConnectionStringBuilder(
                rawPlatformAuditReaderConnectionString)
            {
                ApplicationName = "ToiNoMori.Mvs01.PlatformAuditReader",
                IncludeErrorDetail = false,
                LogParameters = false
            };
            if (builder.Environment.IsProduction()
                && (platformAuditWriterConnectionString.SslMode != SslMode.VerifyFull
                    || platformAuditReaderConnectionString.SslMode != SslMode.VerifyFull))
            {
                throw new InvalidOperationException(
                    "Production PostgreSQL platform audit connections must use SSL Mode=VerifyFull.");
            }

            var platformAuditWriterRole = platformAuditWriterConnectionString.Username
                ?? throw new InvalidOperationException(
                    "PostgreSQL platform audit writer requires an explicit username.");
            var platformAuditReaderRole = platformAuditReaderConnectionString.Username
                ?? throw new InvalidOperationException(
                    "PostgreSQL platform audit reader requires an explicit username.");
            var separatedRoles = new[]
            {
                applicationConnectionString.Username,
                migrationConnectionString.Username,
                platformAuditWriterRole,
                platformAuditReaderRole
            };
            if (separatedRoles.Any(string.IsNullOrWhiteSpace)
                || separatedRoles.Distinct(StringComparer.OrdinalIgnoreCase).Count() != separatedRoles.Length)
            {
                throw new InvalidOperationException(
                    "PostgreSQL application, migration, platform audit writer, and platform audit reader must use four different explicit roles.");
            }

            builder.Services.AddSingleton(new PostgreSqlPersistenceSettings(
                applicationConnectionString.Username,
                platformAuditWriterRole,
                platformAuditReaderRole));
            builder.Services.AddSingleton(new PostgreSqlApplicationDataSource(
                NpgsqlDataSource.Create(applicationConnectionString.ConnectionString)));
            builder.Services.AddSingleton(new PostgreSqlMigrationDataSource(
                NpgsqlDataSource.Create(migrationConnectionString.ConnectionString)));
            builder.Services.AddSingleton(new PostgreSqlPlatformAuditWriterDataSource(
                NpgsqlDataSource.Create(platformAuditWriterConnectionString.ConnectionString)));
            builder.Services.AddSingleton(new PostgreSqlPlatformAuditReaderDataSource(
                NpgsqlDataSource.Create(platformAuditReaderConnectionString.ConnectionString)));
            builder.Services.AddSingleton<PostgreSqlMigrator>();
            builder.Services.AddSingleton<PostgreSqlRoleBoundaryValidator>();
            builder.Services.AddSingleton<IQuestionStore, PostgreSqlQuestionStore>();
            builder.Services.AddSingleton<IAuditSink, PostgreSqlPlatformSecurityAuditSink>();
            builder.Services.AddSingleton<IPlatformSecurityEventReader, PostgreSqlPlatformSecurityEventReader>();
        }
        else
        {
            throw new InvalidOperationException($"Unsupported persistence provider: {provider}");
        }

        builder.Services.AddHostedService<PersistenceInitializerHostedService>();
    }
}
