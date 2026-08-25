using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Diagnostics;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using ToiNoMori.Api;
using ToiNoMori.Api.Tests;
using ToiNoMori.Domain;
using ToiNoMori.Testing;

await using var fixture = await ApiFixture.StartAsync();

var tests = new List<SpecTest>
{
    new("TC-ACC-MVS01-001", "REQ-MVS01-IAM-001", "未認証の更新要求を401で拒否", async () =>
    {
        using var client = fixture.AnonymousClient();
        var response = await client.PostAsJsonAsync("/api/admin/questions", ValidContent("anonymous"));
        SpecAssert.Equal(HttpStatusCode.Unauthorized, response.StatusCode, "Anonymous create must be rejected.");
    }),
    new("TC-ACC-MVS01-002", "REQ-MVS01-IAM-001", "テスト用ヘッダー認証を通常環境で無効化", async () =>
    {
        await using var development = await ApiFixture.StartAsync("Development");
        using var client = development.AuthenticatedClient("header-user", "Editor");
        var response = await client.PostAsJsonAsync("/api/admin/questions", ValidContent("disabled test auth"));
        SpecAssert.Equal(
            HttpStatusCode.Unauthorized,
            response.StatusCode,
            "Testing headers must never authenticate in Development.");
    }),
    new("TC-ACC-MVS01-028", "REQ-MVS01-SEC-002", "本番のメモリ永続化を起動時に拒否", () =>
    {
        var options = ProductionOptions(["Persistence:Provider=InMemory"]);
        try
        {
            _ = AppHost.Build(options);
            throw new TestFailureException("Production must reject the in-memory provider.");
        }
        catch (InvalidOperationException exception)
        {
            SpecAssert.True(
                exception.Message.Contains("PostgreSql", StringComparison.Ordinal),
                "The production rejection must identify the required provider.");
        }

        return Task.CompletedTask;
    }),
    new("TC-ACC-MVS01-029", "REQ-MVS01-SEC-002", "本番DBのTLSホスト名検証なしを起動時に拒否", () =>
    {
        var options = ProductionOptions(
        [
            "Persistence:Provider=PostgreSql",
            "ConnectionStrings:PostgreSql=Host=db.invalid;Database=app;Username=app;SSL Mode=Require"
        ]);
        try
        {
            _ = AppHost.Build(options);
            throw new TestFailureException("Production must require full certificate and host verification.");
        }
        catch (InvalidOperationException exception)
        {
            SpecAssert.True(
                exception.Message.Contains("VerifyFull", StringComparison.Ordinal),
                "The production rejection must require VerifyFull.");
        }

        return Task.CompletedTask;
    }),
    new("TC-ACC-MVS01-066-API", "ADR-0007-D1,ADR-0007-D3", "本番DBのmigration/application接続分離を強制", () =>
    {
        using var dataProtection = TemporaryDataProtectionMaterial.Create();
        var options = ProductionOptions(
        [
            "Persistence:Provider=PostgreSql",
            "ConnectionStrings:PostgreSql=Host=db.invalid;Database=app;Username=mvs01_app;SSL Mode=VerifyFull",
            "Authentication:Mode=Oidc",
            "Authentication:Oidc:Authority=https://identity.example",
            "Authentication:Oidc:ClientId=toi-no-mori-test",
            "Authentication:Oidc:ClientSecret=not-a-real-secret",
            $"DataProtection:KeyRingPath={dataProtection.KeyRingPath}",
            $"DataProtection:CertificatePath={dataProtection.CertificatePath}",
            $"DataProtection:CertificatePassword={dataProtection.CertificatePassword}"
        ]);
        var exception = SpecAssert.Throws<InvalidOperationException>(
            () => _ = AppHost.Build(options),
            "Production must reject a PostgreSQL configuration without a separate migration credential.");
        SpecAssert.True(
            exception.Message.Contains("PostgreSqlMigrator", StringComparison.Ordinal),
            "The rejection must identify the missing migration connection setting without disclosing credentials.");

        var sameRoleOptions = ProductionOptions(
        [
            "Persistence:Provider=PostgreSql",
            "ConnectionStrings:PostgreSql=Host=db.invalid;Database=app;Username=mvs01_app;SSL Mode=VerifyFull",
            "ConnectionStrings:PostgreSqlMigrator=Host=db.invalid;Database=app;Username=mvs01_app;SSL Mode=VerifyFull",
            "Authentication:Mode=Oidc",
            "Authentication:Oidc:Authority=https://identity.example",
            "Authentication:Oidc:ClientId=toi-no-mori-test",
            "Authentication:Oidc:ClientSecret=not-a-real-secret",
            $"DataProtection:KeyRingPath={dataProtection.KeyRingPath}",
            $"DataProtection:CertificatePath={dataProtection.CertificatePath}",
            $"DataProtection:CertificatePassword={dataProtection.CertificatePassword}"
        ]);
        var sameRoleException = SpecAssert.Throws<InvalidOperationException>(
            () => _ = AppHost.Build(sameRoleOptions),
            "Production must reject identical application and migration roles.");
        SpecAssert.True(
            sameRoleException.Message.Contains("different roles", StringComparison.Ordinal),
            "The rejection must identify the separation rule without disclosing credentials.");

        var weakMigrationTlsOptions = ProductionOptions(
        [
            "Persistence:Provider=PostgreSql",
            "ConnectionStrings:PostgreSql=Host=db.invalid;Database=app;Username=mvs01_app;SSL Mode=VerifyFull",
            "ConnectionStrings:PostgreSqlMigrator=Host=db.invalid;Database=app;Username=mvs01_migrator;SSL Mode=Require",
            "Authentication:Mode=Oidc",
            "Authentication:Oidc:Authority=https://identity.example",
            "Authentication:Oidc:ClientId=toi-no-mori-test",
            "Authentication:Oidc:ClientSecret=not-a-real-secret",
            $"DataProtection:KeyRingPath={dataProtection.KeyRingPath}",
            $"DataProtection:CertificatePath={dataProtection.CertificatePath}",
            $"DataProtection:CertificatePassword={dataProtection.CertificatePassword}"
        ]);
        var weakMigrationTlsException = SpecAssert.Throws<InvalidOperationException>(
            () => _ = AppHost.Build(weakMigrationTlsOptions),
            "Production migration connections must also verify certificate hostnames.");
        SpecAssert.True(
            weakMigrationTlsException.Message.Contains("VerifyFull", StringComparison.Ordinal),
            "The migration TLS rejection must require VerifyFull.");
        return Task.CompletedTask;
    }),
    new("TC-ACC-MVS01-034", "REQ-MVS01-IAM-002", "本番のOIDC未設定を起動時に拒否", () =>
    {
        var options = ProductionOptions(
        [
            "Persistence:Provider=PostgreSql",
            "ConnectionStrings:PostgreSql=Host=db.invalid;Database=app;Username=app;SSL Mode=VerifyFull",
            "ConnectionStrings:PostgreSqlMigrator=Host=db.invalid;Database=app;Username=migrator;SSL Mode=VerifyFull",
            "ConnectionStrings:PostgreSqlPlatformAuditWriter=Host=db.invalid;Database=app;Username=platform_writer;SSL Mode=VerifyFull",
            "ConnectionStrings:PostgreSqlPlatformAuditReader=Host=db.invalid;Database=app;Username=platform_reader;SSL Mode=VerifyFull",
            "Audit:PartitionHashKey=MDEyMzQ1Njc4OWFiY2RlZjAxMjM0NTY3ODlhYmNkZWY=",
            "Authentication:Mode=Disabled"
        ]);
        var exception = SpecAssert.Throws<InvalidOperationException>(
            () => _ = AppHost.Build(options),
            "Production must reject disabled interactive authentication.");
        SpecAssert.True(
            exception.Message.Contains("Authentication:Mode=Oidc", StringComparison.Ordinal),
            "The production rejection must identify the required OIDC mode.");
        return Task.CompletedTask;
    }),
    new("TC-ACC-MVS01-035", "REQ-MVS01-SEC-003", "OIDC code+PKCEと安全なBFF Cookieを強制", async () =>
    {
        using var dataProtection = TemporaryDataProtectionMaterial.Create();
        var options = ProductionOptions(
        [
            "Persistence:Provider=PostgreSql",
            "ConnectionStrings:PostgreSql=Host=db.invalid;Database=app;Username=app;SSL Mode=VerifyFull",
            "ConnectionStrings:PostgreSqlMigrator=Host=db.invalid;Database=app;Username=migrator;SSL Mode=VerifyFull",
            "ConnectionStrings:PostgreSqlPlatformAuditWriter=Host=db.invalid;Database=app;Username=platform_writer;SSL Mode=VerifyFull",
            "ConnectionStrings:PostgreSqlPlatformAuditReader=Host=db.invalid;Database=app;Username=platform_reader;SSL Mode=VerifyFull",
            "Audit:PartitionHashKey=MDEyMzQ1Njc4OWFiY2RlZjAxMjM0NTY3ODlhYmNkZWY=",
            "Authentication:Mode=Oidc",
            "Authentication:Oidc:Authority=https://identity.example",
            "Authentication:Oidc:ClientId=toi-no-mori-test",
            "Authentication:Oidc:ClientSecret=not-a-real-secret",
            $"DataProtection:KeyRingPath={dataProtection.KeyRingPath}",
            $"DataProtection:CertificatePath={dataProtection.CertificatePath}",
            $"DataProtection:CertificatePassword={dataProtection.CertificatePassword}"
        ]);
        await using var production = AppHost.Build(options);
        var cookie = production.Services
            .GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>()
            .Get(BffAuthentication.CookieScheme);
        var oidc = production.Services
            .GetRequiredService<IOptionsMonitor<OpenIdConnectOptions>>()
            .Get(BffAuthentication.OidcScheme);
        var authentication = production.Services
            .GetRequiredService<IOptions<AuthenticationOptions>>()
            .Value;

        SpecAssert.Equal("__Host-toi-no-mori-session", cookie.Cookie.Name, "Session cookie must use a host-only prefix.");
        SpecAssert.True(cookie.Cookie.HttpOnly, "Session cookie must be HttpOnly.");
        SpecAssert.Equal(CookieSecurePolicy.Always, cookie.Cookie.SecurePolicy, "Session cookie must require HTTPS.");
        SpecAssert.Equal(SameSiteMode.Lax, cookie.Cookie.SameSite, "Session cookie must use SameSite=Lax.");
        SpecAssert.Equal(TimeSpan.FromMinutes(20), cookie.ExpireTimeSpan, "Session lifetime must be bounded.");
        SpecAssert.False(cookie.SlidingExpiration, "Session must not extend indefinitely.");
        SpecAssert.Equal(
            BffAuthentication.CookieScheme,
            authentication.DefaultChallengeScheme,
            "Unauthenticated API requests must return Cookie-handler 401 instead of starting OIDC implicitly.");
        SpecAssert.Equal(OpenIdConnectResponseType.Code, oidc.ResponseType, "OIDC must use authorization code flow.");
        SpecAssert.True(oidc.UsePkce, "OIDC must use PKCE.");
        SpecAssert.False(oidc.SaveTokens, "OIDC tokens must not be stored in the browser session ticket.");
        SpecAssert.True(oidc.RequireHttpsMetadata, "OIDC metadata must require HTTPS.");
        SpecAssert.Equal(TimeSpan.FromMinutes(15), oidc.MaxAge, "OIDC sign-in must request recent authentication.");
        SpecAssert.True(oidc.TokenValidationParameters.RequireSignedTokens, "ID tokens must be signed.");
        SpecAssert.True(oidc.TokenValidationParameters.ValidateAudience, "The client audience must be validated.");
        SpecAssert.True(oidc.TokenValidationParameters.ValidateLifetime, "ID token lifetime must be validated.");
    }),
    new("TC-ACC-MVS01-036", "REQ-MVS01-IAM-003", "MFA証跡のない管理要求を403で拒否", async () =>
    {
        using var client = fixture.AuthenticatedClient(
            "editor-without-mfa",
            "Editor",
            includeMfa: false);
        var response = await client.PostAsJsonAsync("/api/admin/questions", ValidContent("missing mfa"));
        SpecAssert.Equal(HttpStatusCode.Forbidden, response.StatusCode, "Administrative writes must require an MFA claim.");
    }),
    new("TC-ACC-MVS01-037", "REQ-MVS01-SEC-003", "BFF sessionは最小情報だけを返す", async () =>
    {
        using var client = fixture.AuthenticatedClient("bff-user", "Editor,Reviewer");
        var response = await client.GetAsync("/bff/session");
        var wire = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(wire).RootElement;
        SpecAssert.Equal(HttpStatusCode.OK, response.StatusCode, "MFA-authenticated session must be available.");
        SpecAssert.Equal("bff-user", json.GetProperty("subject").GetString(), "BFF session must identify the subject.");
        SpecAssert.Equal("test-csrf-token", json.GetProperty("csrfToken").GetString(), "BFF session must return the bound CSRF token.");
        SpecAssert.False(wire.Contains("accessToken", StringComparison.OrdinalIgnoreCase), "Access tokens must not reach browser JSON.");
        SpecAssert.False(wire.Contains("refreshToken", StringComparison.OrdinalIgnoreCase), "Refresh tokens must not reach browser JSON.");
        SpecAssert.True(response.Headers.CacheControl?.NoStore == true, "BFF session must never be cached.");
    }),
    new("TC-ACC-MVS01-038", "REQ-MVS01-SEC-003", "BFF return URLを同一アプリ内へ限定", () =>
    {
        SpecAssert.Equal("/app/", BffEndpointMappings.NormalizeReturnUrl("https://evil.example/"), "Absolute redirects must be rejected.");
        SpecAssert.Equal("/app/", BffEndpointMappings.NormalizeReturnUrl("//evil.example/"), "Protocol-relative redirects must be rejected.");
        SpecAssert.Equal("/app/", BffEndpointMappings.NormalizeReturnUrl("/app\\evil"), "Backslash redirects must be rejected.");
        SpecAssert.Equal("/app/?query=safe", BffEndpointMappings.NormalizeReturnUrl("/app/?query=safe"), "Local app redirects must be preserved.");
        return Task.CompletedTask;
    }),
    new("TC-ACC-MVS01-003", "REQ-MVS01-QST-001", "有効な問いをDRAFTとして登録", async () =>
    {
        using var client = fixture.AuthenticatedClient("editor-create", "Editor");
        var response = await client.PostAsJsonAsync("/api/admin/questions", ValidContent("created"));
        SpecAssert.Equal(HttpStatusCode.Created, response.StatusCode, "Valid create must return 201.");
        var question = await ReadQuestionAsync(response);
        SpecAssert.Equal(QuestionStatus.Draft, question.Status, "Created question must be DRAFT.");
        SpecAssert.Equal(1, question.Version, "Created question must have version one.");
        SpecAssert.Equal("\"1\"", response.Headers.ETag?.Tag, "Create must return the version ETag.");
    }),
    new("TC-ACC-MVS01-004", "REQ-MVS01-QST-001", "タイトル長の境界違反を400で拒否", async () =>
    {
        using var client = fixture.AuthenticatedClient("editor-boundary", "Editor");
        var invalid = new QuestionContentRequest(new string('x', 121), "body", []);
        var response = await client.PostAsJsonAsync("/api/admin/questions", invalid);
        SpecAssert.Equal(HttpStatusCode.BadRequest, response.StatusCode, "Oversized title must be rejected.");
    }),
    new("TC-ACC-MVS01-005", "REQ-MVS01-SEC-001", "HTML入力をJSONとして安全にエンコード", async () =>
    {
        using var client = fixture.AuthenticatedClient("editor-xss", "Editor");
        var response = await client.PostAsJsonAsync(
            "/api/admin/questions",
            new QuestionContentRequest("xss", "<script>alert('x')</script>", ["security"]));
        var wire = await response.Content.ReadAsStringAsync();
        SpecAssert.Equal(HttpStatusCode.Created, response.StatusCode, "Plain-text input must remain valid data.");
        SpecAssert.False(wire.Contains("<script>", StringComparison.OrdinalIgnoreCase), "Raw script markup must not appear on the wire.");
        SpecAssert.True(
            response.Content.Headers.ContentType?.MediaType == "application/json",
            "The response must be JSON, never HTML.");
    }),
    new("TC-ACC-MVS01-006", "REQ-MVS01-QST-002", "所有者がIf-Match一致時に更新", async () =>
    {
        using var client = fixture.AuthenticatedClient("editor-update", "Editor");
        var created = await CreateDraftAsync(client, "before");
        using var request = new HttpRequestMessage(HttpMethod.Put, $"/api/admin/questions/{created.Id}")
        {
            Content = JsonContent.Create(ValidContent("after"))
        };
        request.Headers.TryAddWithoutValidation("If-Match", "\"1\"");
        var response = await client.SendAsync(request);
        var updated = await ReadQuestionAsync(response);
        SpecAssert.Equal(HttpStatusCode.OK, response.StatusCode, "Owner update must succeed.");
        SpecAssert.Equal(2, updated.Version, "Update must increment the version.");
    }),
    new("TC-ACC-MVS01-007", "REQ-MVS01-QST-002", "古いIf-Matchを409で拒否", async () =>
    {
        using var client = fixture.AuthenticatedClient("editor-conflict", "Editor");
        var created = await CreateDraftAsync(client, "conflict");
        var first = await PutAsync(client, created.Id, 1, ValidContent("v2"));
        SpecAssert.Equal(HttpStatusCode.OK, first.StatusCode, "The first update must succeed.");
        var stale = await PutAsync(client, created.Id, 1, ValidContent("stale"));
        SpecAssert.Equal(HttpStatusCode.Conflict, stale.StatusCode, "A stale update must return 409.");
    }),
    new("TC-ACC-MVS01-009", "REQ-MVS01-WF-001", "未定義遷移を409で拒否", async () =>
    {
        using var client = fixture.AuthenticatedClient("editor-transition", "Editor");
        var created = await CreateDraftAsync(client, "transition");
        var first = await client.PostAsync($"/api/admin/questions/{created.Id}/submit", null);
        SpecAssert.Equal(HttpStatusCode.OK, first.StatusCode, "The first submit must succeed.");
        var second = await client.PostAsync($"/api/admin/questions/{created.Id}/submit", null);
        SpecAssert.Equal(HttpStatusCode.Conflict, second.StatusCode, "The second submit must return 409.");
    }),
    new("TC-ACC-MVS01-010", "REQ-MVS01-WF-002", "API境界でも自己承認を403で拒否", async () =>
    {
        using var editor = fixture.AuthenticatedClient("same-person", "Editor");
        var created = await CreateDraftAsync(editor, "self approval");
        await editor.PostAsync($"/api/admin/questions/{created.Id}/submit", null);
        using var reviewer = fixture.AuthenticatedClient("same-person", "Reviewer");
        var response = await ApproveAsync(reviewer, created.Id, $"approve-{created.Id}", expectedVersion: 2);
        SpecAssert.Equal(HttpStatusCode.Forbidden, response.StatusCode, "Self approval must return 403.");
    }),
    new("TC-ACC-MVS01-011", "REQ-MVS01-WF-002", "Reviewer権限のない承認を403で拒否", async () =>
    {
        using var editor = fixture.AuthenticatedClient("editor-no-review", "Editor");
        var created = await CreateDraftAsync(editor, "role test");
        await editor.PostAsync($"/api/admin/questions/{created.Id}/submit", null);
        var response = await ApproveAsync(editor, created.Id, $"approve-{created.Id}", expectedVersion: 2);
        SpecAssert.Equal(HttpStatusCode.Forbidden, response.StatusCode, "An editor must not approve.");
    }),
    new("TC-ACC-MVS01-064-API", "ADR-0008-D1", "承認対象版をIf-Matchで固定", async () =>
    {
        using var editor = fixture.AuthenticatedClient("version-owner", "Editor");
        var created = await CreateDraftAsync(editor, "version-bound approval");
        using var submittedResponse = await editor.PostAsync($"/api/admin/questions/{created.Id}/submit", null);
        var submitted = await ReadQuestionAsync(submittedResponse);
        SpecAssert.Equal(2, submitted.Version, "Submit must establish reviewed version two.");

        using var reviewer = fixture.AuthenticatedClient("version-reviewer", "Reviewer");
        using var missing = await ApproveWithIfMatchAsync(
            reviewer,
            created.Id,
            $"missing-{created.Id}",
            ifMatch: null);
        SpecAssert.Equal(
            (HttpStatusCode)StatusCodes.Status428PreconditionRequired,
            missing.StatusCode,
            "Approval without If-Match must return 428.");

        using var malformed = await ApproveWithIfMatchAsync(
            reviewer,
            created.Id,
            $"malformed-{created.Id}",
            "W/\"2\"");
        SpecAssert.Equal(HttpStatusCode.BadRequest, malformed.StatusCode, "A weak or malformed approval ETag must return 400.");

        using var stale = await ApproveWithIfMatchAsync(
            reviewer,
            created.Id,
            $"stale-{created.Id}",
            "\"1\"");
        SpecAssert.Equal(HttpStatusCode.Conflict, stale.StatusCode, "A stale approval ETag must return 409.");

        using var unchangedResponse = await reviewer.GetAsync($"/api/admin/questions/{created.Id}");
        var unchanged = await ReadQuestionAsync(unchangedResponse);
        SpecAssert.Equal(QuestionStatus.InReview, unchanged.Status, "Rejected approval must keep IN_REVIEW.");
        SpecAssert.Equal(submitted.Version, unchanged.Version, "Rejected approval must not advance the version.");
        SpecAssert.Equal("\"2\"", unchangedResponse.Headers.ETag?.Tag, "Rejected approval must preserve the reviewed ETag.");

        var successKey = $"current-{created.Id}";
        using var current = await ApproveWithIfMatchAsync(
            reviewer,
            created.Id,
            successKey,
            "\"2\"");
        var published = await ReadQuestionAsync(current);
        SpecAssert.Equal(HttpStatusCode.OK, current.StatusCode, "The current reviewed version must be approved.");
        SpecAssert.Equal(QuestionStatus.Published, published.Status, "Approval must publish the question.");
        SpecAssert.Equal(3, published.Version, "Approval must advance the aggregate version once.");
        SpecAssert.Equal("\"3\"", current.Headers.ETag?.Tag, "Approval response must return the new ETag.");

        using var retry = await ApproveWithIfMatchAsync(
            reviewer,
            created.Id,
            successKey,
            "\"2\"");
        var retryResult = await ReadQuestionAsync(retry);
        SpecAssert.Equal(HttpStatusCode.OK, retry.StatusCode, "An identical idempotent retry must return the stored result.");
        SpecAssert.Equal(published.Version, retryResult.Version, "An identical retry must not approve twice.");
        SpecAssert.Equal("\"3\"", retry.Headers.ETag?.Tag, "An idempotent retry must return the stored result ETag.");

        using var changedFingerprint = await ApproveWithIfMatchAsync(
            reviewer,
            created.Id,
            successKey,
            "\"3\"");
        SpecAssert.Equal(
            HttpStatusCode.Conflict,
            changedFingerprint.StatusCode,
            "Reusing an idempotency key for a different approval version must return 409.");
    }),
    new("TC-ACC-MVS01-065-API", "ADR-0007-D2", "外部組織claimを内部tenantへ許可表変換", async () =>
    {
        using var missingClaim = fixture.AuthenticatedClient(
            "missing-tenant-user",
            "Editor",
            externalOrganizationId: null);
        using var missingResponse = await missingClaim.PostAsJsonAsync(
            "/api/admin/questions",
            ValidContent("missing tenant claim"));
        var missingWire = await missingResponse.Content.ReadAsStringAsync();
        SpecAssert.Equal(HttpStatusCode.Forbidden, missingResponse.StatusCode, "A missing organization claim must fail closed.");
        SpecAssert.True(
            missingWire.Contains("tenant-claim-missing", StringComparison.Ordinal),
            "The rejection must expose only the stable missing-claim problem code.");

        using var unmappedClaim = fixture.AuthenticatedClient(
            "unmapped-tenant-user",
            "Editor",
            externalOrganizationId: "org-not-registered");
        using var unmappedResponse = await unmappedClaim.PostAsJsonAsync(
            "/api/admin/questions",
            ValidContent("unmapped tenant claim"));
        var unmappedWire = await unmappedResponse.Content.ReadAsStringAsync();
        SpecAssert.Equal(HttpStatusCode.Forbidden, unmappedResponse.StatusCode, "An unmapped organization claim must fail closed.");
        SpecAssert.True(
            unmappedWire.Contains("tenant-claim-invalid-or-unmapped", StringComparison.Ordinal),
            "The rejection must not disclose the supplied external organization value.");
        SpecAssert.False(
            unmappedWire.Contains("org-not-registered", StringComparison.Ordinal),
            "The external organization identifier must not be reflected.");

        using var duplicateClaim = fixture.AuthenticatedClient(
            "duplicate-tenant-user",
            "Editor",
            externalOrganizationId: "org-mvs01,org-other");
        using var duplicateResponse = await duplicateClaim.PostAsJsonAsync(
            "/api/admin/questions",
            ValidContent("duplicate tenant claim"));
        SpecAssert.Equal(
            HttpStatusCode.Forbidden,
            duplicateResponse.StatusCode,
            "Multiple organization claims must fail closed even when both values are registered.");

        using var wrongIssuer = fixture.AuthenticatedClient(
            "wrong-issuer-user",
            "Editor",
            externalOrganizationId: "org-mvs01",
            verifiedIssuer: "https://unregistered-issuer.example");
        using var wrongIssuerResponse = await wrongIssuer.PostAsJsonAsync(
            "/api/admin/questions",
            ValidContent("wrong issuer"));
        SpecAssert.Equal(
            HttpStatusCode.Forbidden,
            wrongIssuerResponse.StatusCode,
            "A registered organization value from another issuer must fail closed.");

        using var mappedClaim = fixture.AuthenticatedClient(
            "mapped-tenant-user",
            "Editor",
            externalOrganizationId: "org-mvs01");
        using var mappedResponse = await mappedClaim.PostAsJsonAsync(
            "/api/admin/questions",
            ValidContent("mapped tenant claim"));
        SpecAssert.Equal(HttpStatusCode.Created, mappedResponse.StatusCode, "A registered organization claim must resolve.");

        using var otherTenantEditor = fixture.AuthenticatedClient(
            "private-other-owner",
            "Editor",
            externalOrganizationId: "org-other");
        var otherTenantQuestion = await CreateDraftAsync(otherTenantEditor, "other tenant public boundary");
        using var otherTenantSubmit = await otherTenantEditor.PostAsync(
            $"/api/admin/questions/{otherTenantQuestion.Id}/submit",
            null);
        SpecAssert.Equal(HttpStatusCode.OK, otherTenantSubmit.StatusCode, "Other-tenant public precondition must submit.");
        using var otherTenantReviewer = fixture.AuthenticatedClient(
            "private-other-reviewer",
            "Reviewer",
            externalOrganizationId: "org-other");
        using var otherTenantApprove = await ApproveAsync(
            otherTenantReviewer,
            otherTenantQuestion.Id,
            $"other-public-{otherTenantQuestion.Id}",
            expectedVersion: 2);
        SpecAssert.Equal(HttpStatusCode.OK, otherTenantApprove.StatusCode, "Other-tenant public precondition must approve.");
        using var anonymous = fixture.AnonymousClient();
        using var hiddenFromDefaultPublic = await anonymous.GetAsync(
            $"/api/public/questions/{otherTenantQuestion.Id}");
        SpecAssert.Equal(
            HttpStatusCode.NotFound,
            hiddenFromDefaultPublic.StatusCode,
            "Anonymous public routing must remain pinned to the server-configured MVS-01 tenant.");
    }),
    new("TC-ACC-MVS01-069-API", "RV-021", "他tenantと他所有者を同一404へ正規化", async () =>
    {
        using var tenantAOwner = fixture.AuthenticatedClient(
            "tenant-a-owner",
            "Editor",
            externalOrganizationId: "org-mvs01");
        var created = await CreateDraftAsync(tenantAOwner, "tenant visibility");

        using var sameTenantOtherOwner = fixture.AuthenticatedClient(
            "tenant-a-other-editor",
            "Editor",
            externalOrganizationId: "org-mvs01");
        using var sameTenantHidden = await PutAsync(
            sameTenantOtherOwner,
            created.Id,
            created.Version,
            ValidContent("same tenant BOLA"));
        SpecAssert.Equal(
            HttpStatusCode.NotFound,
            sameTenantHidden.StatusCode,
            "A non-owner must receive the normalized 404 response.");
        var sameTenantProblem = JsonDocument.Parse(
            await sameTenantHidden.Content.ReadAsStringAsync()).RootElement;

        using var otherTenantReviewer = fixture.AuthenticatedClient(
            "tenant-b-reviewer",
            "Reviewer",
            externalOrganizationId: "org-other");
        using var crossTenantHidden = await otherTenantReviewer.GetAsync(
            $"/api/admin/questions/{created.Id}");
        SpecAssert.Equal(
            HttpStatusCode.NotFound,
            crossTenantHidden.StatusCode,
            "A Reviewer from another tenant must not enumerate the question.");
        var crossTenantProblem = JsonDocument.Parse(
            await crossTenantHidden.Content.ReadAsStringAsync()).RootElement;
        SpecAssert.Equal(
            sameTenantProblem.GetProperty("type").GetString(),
            crossTenantProblem.GetProperty("type").GetString(),
            "Same-tenant ownership denial and cross-tenant invisibility must have the same problem type.");
        SpecAssert.Equal(
            sameTenantProblem.GetProperty("title").GetString(),
            crossTenantProblem.GetProperty("title").GetString(),
            "Same-tenant ownership denial and cross-tenant invisibility must have the same problem title.");
        var crossTenantWire = crossTenantProblem.GetRawText();
        SpecAssert.False(
            crossTenantWire.Contains(created.Id.ToString(), StringComparison.OrdinalIgnoreCase),
            "The normalized problem must disclose no resource identifier.");

        using var sameTenantReviewer = fixture.AuthenticatedClient(
            "tenant-a-reviewer",
            "Reviewer",
            externalOrganizationId: "org-mvs01");
        using var sameTenantVisible = await sameTenantReviewer.GetAsync(
            $"/api/admin/questions/{created.Id}");
        SpecAssert.Equal(
            HttpStatusCode.OK,
            sameTenantVisible.StatusCode,
            "A Reviewer in the same tenant must retain access.");
    }),
    new("TC-ACC-MVS01-014", "REQ-MVS01-PUB-001", "承認済みだけを公開DTOで取得", async () =>
    {
        var published = await PublishAsync(fixture, "public question", "public-owner", "public-reviewer");
        using var client = fixture.AnonymousClient();
        var response = await client.GetAsync($"/api/public/questions/{published.Id}");
        var wire = await response.Content.ReadAsStringAsync();
        SpecAssert.Equal(HttpStatusCode.OK, response.StatusCode, "Published detail must be public.");
        SpecAssert.False(wire.Contains("ownerSubject", StringComparison.Ordinal), "Public DTO must omit ownerSubject.");
        SpecAssert.False(wire.Contains("\"version\"", StringComparison.Ordinal), "Public DTO must omit internal version.");
    }),
    new("TC-ACC-MVS01-017", "REQ-MVS01-WD-001", "取り下げ後は一般経路から404", async () =>
    {
        var published = await PublishAsync(fixture, "withdraw question", "withdraw-owner", "withdraw-reviewer");
        using var reviewer = fixture.AuthenticatedClient("withdraw-reviewer", "Reviewer");
        var withdrawn = await reviewer.PostAsJsonAsync(
            $"/api/admin/questions/{published.Id}/withdraw",
            new ReviewReasonRequest("公開終了"));
        SpecAssert.Equal(HttpStatusCode.OK, withdrawn.StatusCode, "Withdraw must succeed.");
        using var publicClient = fixture.AnonymousClient();
        var publicResponse = await publicClient.GetAsync($"/api/public/questions/{published.Id}");
        SpecAssert.Equal(HttpStatusCode.NotFound, publicResponse.StatusCode, "Withdrawn detail must return 404.");
    }),
    new("TC-ACC-MVS01-018", "REQ-MVS01-SEC-001", "同一冪等キーの承認を一回だけ確定", async () =>
    {
        using var editor = fixture.AuthenticatedClient("idem-owner", "Editor");
        var created = await CreateDraftAsync(editor, "idempotency");
        await editor.PostAsync($"/api/admin/questions/{created.Id}/submit", null);
        using var reviewer = fixture.AuthenticatedClient("idem-reviewer", "Reviewer");
        var key = $"approve-{created.Id}";
        var first = await ApproveAsync(reviewer, created.Id, key, expectedVersion: 2);
        var firstResult = await ReadQuestionAsync(first);
        var second = await ApproveAsync(reviewer, created.Id, key, expectedVersion: 2);
        var secondResult = await ReadQuestionAsync(second);
        SpecAssert.Equal(firstResult.Version, secondResult.Version, "Retry must return the original result.");
        var auditCount = fixture.Store.ReadAudit().Count(record =>
            record.TargetId == created.Id
            && record.Action == "question.approve"
            && record.Result == "success");
        SpecAssert.Equal(1, auditCount, "Approval must be committed and audited once.");
    }),
    new("TC-ACC-MVS01-019", "REQ-MVS01-SEC-001", "CSRFヘッダー不備を403で拒否", async () =>
    {
        using var client = fixture.AuthenticatedClient("editor-csrf", "Editor", includeCsrfHeader: false);
        var response = await client.PostAsJsonAsync("/api/admin/questions", ValidContent("csrf"));
        SpecAssert.Equal(HttpStatusCode.Forbidden, response.StatusCode, "Missing CSRF header must return 403.");
    }),
    new("TC-ACC-MVS01-020", "REQ-MVS01-SEC-001", "他編集者の下書き更新を404へ正規化", async () =>
    {
        using var owner = fixture.AuthenticatedClient("owner-a", "Editor");
        var created = await CreateDraftAsync(owner, "owner guard");
        using var attacker = fixture.AuthenticatedClient("editor-b", "Editor");
        var response = await PutAsync(attacker, created.Id, 1, ValidContent("stolen"));
        SpecAssert.Equal(HttpStatusCode.NotFound, response.StatusCode, "BOLA attempt must use the normalized 404 response.");
    }),
    new("TC-ACC-MVS01-023", "REQ-MVS01-AUD-001", "監査記録へ本文・秘密値を保存しない", async () =>
    {
        const string secret = "TOP-SECRET-TOKEN-DO-NOT-LOG";
        using var client = fixture.AuthenticatedClient("editor-audit", "Editor");
        var response = await client.PostAsJsonAsync(
            "/api/admin/questions",
            new QuestionContentRequest("audit", secret, ["security"]));
        var created = await ReadQuestionAsync(response);
        var auditJson = JsonSerializer.Serialize(fixture.Store.ReadAudit().Where(record => record.TargetId == created.Id));
        SpecAssert.False(auditJson.Contains(secret, StringComparison.Ordinal), "Audit must not contain body or secret text.");
        SpecAssert.True(auditJson.Contains("question.create", StringComparison.Ordinal), "Audit must contain the action.");
    }),
    new("TC-ACC-MVS01-015", "REQ-MVS01-SRH-001", "公開問いをキーワード・タグで検索", async () =>
    {
        var published = await PublishAsync(fixture, "needle searchable", "search-owner", "search-reviewer");
        using var client = fixture.AnonymousClient();
        var response = await client.GetAsync("/api/public/questions?query=needle&tag=cloud");
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        SpecAssert.Equal(HttpStatusCode.OK, response.StatusCode, "Public search must succeed.");
        SpecAssert.True(
            json.ValueKind == JsonValueKind.Array
            && json.EnumerateArray().Any(item => item.GetProperty("id").GetGuid() == published.Id),
            "Search must return the matching published question.");
    }),
    new("TC-ACC-MVS01-049", "REQ-MVS01-UI-001", "編集者一覧を本人所有の問いだけに限定", async () =>
    {
        using var owner = fixture.AuthenticatedClient("list-owner", "Editor");
        using var other = fixture.AuthenticatedClient("list-other", "Editor");
        var mine = await CreateDraftAsync(owner, "my managed list");
        _ = await CreateDraftAsync(other, "other managed list");

        using var response = await owner.GetAsync("/api/admin/questions?limit=100");
        var questions = await ReadQuestionsAsync(response);
        SpecAssert.Equal(HttpStatusCode.OK, response.StatusCode, "An Editor may list their managed questions.");
        SpecAssert.True(questions.Any(question => question.Id == mine.Id), "The Editor list must contain their own draft.");
        SpecAssert.True(
            questions.All(question => question.OwnerSubject == "list-owner"),
            "The Editor list must not disclose another owner's question.");
    }),
    new("TC-ACC-MVS01-050", "REQ-MVS01-UI-002", "Reviewer一覧でレビュー待ちを取得", async () =>
    {
        using var editor = fixture.AuthenticatedClient("queue-owner", "Editor");
        var created = await CreateDraftAsync(editor, "review queue");
        using var submitted = await editor.PostAsync($"/api/admin/questions/{created.Id}/submit", null);
        SpecAssert.Equal(HttpStatusCode.OK, submitted.StatusCode, "The review-queue precondition must submit.");

        using var reviewer = fixture.AuthenticatedClient("queue-reviewer", "Reviewer");
        using var response = await reviewer.GetAsync("/api/admin/questions?status=IN_REVIEW&limit=100");
        var questions = await ReadQuestionsAsync(response);
        SpecAssert.Equal(HttpStatusCode.OK, response.StatusCode, "A Reviewer may list the review queue.");
        SpecAssert.True(questions.Any(question => question.Id == created.Id), "The submitted question must enter the review queue.");
        SpecAssert.True(questions.All(question => question.Status == QuestionStatus.InReview), "The status filter must be enforced.");
    }),
    new("TC-ACC-MVS01-051", "REQ-MVS01-SEC-006", "管理詳細を所有者またはReviewerだけに限定", async () =>
    {
        using var owner = fixture.AuthenticatedClient("detail-owner", "Editor");
        var created = await CreateDraftAsync(owner, "managed detail");
        using var ownerResponse = await owner.GetAsync($"/api/admin/questions/{created.Id}");
        SpecAssert.Equal(HttpStatusCode.OK, ownerResponse.StatusCode, "The owner may read their administrative detail.");
        SpecAssert.Equal("\"1\"", ownerResponse.Headers.ETag?.Tag, "Administrative detail must expose the edit version as an ETag.");

        using var other = fixture.AuthenticatedClient("detail-other", "Editor");
        using var hidden = await other.GetAsync($"/api/admin/questions/{created.Id}");
        SpecAssert.Equal(HttpStatusCode.NotFound, hidden.StatusCode, "Another Editor must not enumerate the draft.");

        using var reviewer = fixture.AuthenticatedClient("detail-reviewer", "Reviewer");
        using var visible = await reviewer.GetAsync($"/api/admin/questions/{created.Id}");
        SpecAssert.Equal(HttpStatusCode.OK, visible.StatusCode, "A Reviewer may inspect the draft detail.");
    }),
    new("TC-ACC-MVS01-052", "REQ-MVS01-UI-003", "作成から承認公開までをAPI受入フローで完結", async () =>
    {
        using var editor = fixture.AuthenticatedClient("flow-editor", "Editor");
        var created = await CreateDraftAsync(editor, "stage6 full flow");
        using var updatedResponse = await PutAsync(
            editor,
            created.Id,
            created.Version,
            new QuestionContentRequest("Stage 6 public question", "スマートフォン業務フロー", ["stage6"]));
        var updated = await ReadQuestionAsync(updatedResponse);
        SpecAssert.Equal(2, updated.Version, "The Editor update must advance the version.");

        using var submitted = await editor.PostAsync($"/api/admin/questions/{created.Id}/submit", null);
        SpecAssert.Equal(HttpStatusCode.OK, submitted.StatusCode, "The Editor must submit the updated draft.");
        using var reviewer = fixture.AuthenticatedClient("flow-reviewer", "Reviewer");
        using var approved = await ApproveAsync(
            reviewer,
            created.Id,
            $"stage6-{created.Id}",
            expectedVersion: updated.Version + 1);
        var published = await ReadQuestionAsync(approved);
        SpecAssert.Equal(QuestionStatus.Published, published.Status, "A distinct Reviewer must publish the question.");

        using var anonymous = fixture.AnonymousClient();
        using var publicResponse = await anonymous.GetAsync($"/api/public/questions/{created.Id}");
        SpecAssert.Equal(HttpStatusCode.OK, publicResponse.StatusCode, "The approved question must be publicly readable.");
    }),
    new("TC-ACC-MVS01-053", "REQ-MVS01-UI-004", "差し戻し理由を編集者の再作業へ引き継ぐ", async () =>
    {
        using var editor = fixture.AuthenticatedClient("return-editor", "Editor");
        var created = await CreateDraftAsync(editor, "return flow");
        using var submitted = await editor.PostAsync($"/api/admin/questions/{created.Id}/submit", null);
        SpecAssert.Equal(HttpStatusCode.OK, submitted.StatusCode, "The return-flow precondition must submit.");

        using var reviewer = fixture.AuthenticatedClient("return-reviewer", "Reviewer");
        using var returnedResponse = await reviewer.PostAsJsonAsync(
            $"/api/admin/questions/{created.Id}/return",
            new ReviewReasonRequest("出典を追記してください"));
        var returned = await ReadQuestionAsync(returnedResponse);
        SpecAssert.Equal(QuestionStatus.Draft, returned.Status, "A returned question must become editable again.");
        SpecAssert.Equal("出典を追記してください", returned.ReviewReason, "The Editor must receive the review reason.");

        using var correctedResponse = await PutAsync(editor, created.Id, returned.Version, ValidContent("corrected"));
        var corrected = await ReadQuestionAsync(correctedResponse);
        SpecAssert.True(corrected.ReviewReason is null, "Saving a correction must clear the handled review reason.");
    }),
    new("TC-ACC-MVS01-081-API", "ADR-0008-D4", "role別DTOで理由の可視性を分離", async () =>
    {
        using var editor = fixture.AuthenticatedClient("dto-editor", "Editor");
        var created = await CreateDraftAsync(editor, "role dto");
        using var submitted = await editor.PostAsync($"/api/admin/questions/{created.Id}/submit", null);
        SpecAssert.Equal(HttpStatusCode.OK, submitted.StatusCode, "The role-DTO precondition must submit.");

        using var reviewer = fixture.AuthenticatedClient("dto-reviewer", "Reviewer");
        using var approved = await ApproveAsync(
            reviewer,
            created.Id,
            $"dto-{created.Id}",
            expectedVersion: created.Version + 1);
        SpecAssert.Equal(HttpStatusCode.OK, approved.StatusCode, "The role-DTO precondition must publish.");

        using (var anonymous = fixture.AnonymousClient())
        using (var publicResponse = await anonymous.GetAsync($"/api/public/questions/{created.Id}"))
        {
            var publicJson = await publicResponse.Content.ReadFromJsonAsync<JsonElement>();
            SpecAssert.Equal(HttpStatusCode.OK, publicResponse.StatusCode, "Published content must remain public.");
            SpecAssert.False(publicJson.TryGetProperty("reviewReason", out _), "Public DTO must omit the review reason.");
            SpecAssert.False(publicJson.TryGetProperty("withdrawalReason", out _), "Public DTO must omit the withdrawal reason.");
            SpecAssert.False(publicJson.TryGetProperty("ownerSubject", out _), "Public DTO must omit the owner subject.");
        }

        const string withdrawalReason = "公開根拠の有効期限終了";
        using var withdrawn = await reviewer.PostAsJsonAsync(
            $"/api/admin/questions/{created.Id}/withdraw",
            new ReviewReasonRequest(withdrawalReason));
        SpecAssert.Equal(HttpStatusCode.OK, withdrawn.StatusCode, "Reviewer must withdraw the published question.");

        using (var editorDetail = await editor.GetAsync($"/api/admin/questions/{created.Id}"))
        {
            var editorJson = await editorDetail.Content.ReadFromJsonAsync<JsonElement>();
            SpecAssert.Equal(HttpStatusCode.OK, editorDetail.StatusCode, "The owner Editor may read the withdrawn question.");
            SpecAssert.True(editorJson.TryGetProperty("reviewReason", out _), "Editor DTO must carry its allowlisted review-reason field.");
            SpecAssert.False(editorJson.TryGetProperty("withdrawalReason", out _), "Editor DTO must not disclose the withdrawal reason.");
            SpecAssert.False(editorJson.TryGetProperty("ownerSubject", out _), "Editor DTO must not expose the redundant owner subject.");
        }

        using (var reviewerDetail = await reviewer.GetAsync($"/api/admin/questions/{created.Id}"))
        {
            var reviewerJson = await reviewerDetail.Content.ReadFromJsonAsync<JsonElement>();
            SpecAssert.Equal(HttpStatusCode.OK, reviewerDetail.StatusCode, "Reviewer may read the withdrawn question.");
            SpecAssert.Equal("dto-editor", reviewerJson.GetProperty("ownerSubject").GetString(), "Reviewer DTO must identify the owner for self-approval checks.");
            SpecAssert.Equal(withdrawalReason, reviewerJson.GetProperty("withdrawalReason").GetString(), "Reviewer DTO must expose the withdrawal reason.");
        }
    }),
    new("TC-ACC-MVS01-054", "REQ-MVS01-UI-002", "未定義の管理一覧状態を400で拒否", async () =>
    {
        using var reviewer = fixture.AuthenticatedClient("filter-reviewer", "Reviewer");
        using var response = await reviewer.GetAsync("/api/admin/questions?status=UNKNOWN");
        SpecAssert.Equal(HttpStatusCode.BadRequest, response.StatusCode, "An unknown status filter must be rejected.");
    }),
    new("TC-ACC-MVS01-072-API", "ADR-0009-D7", "tenant監査をAuditor専用の上限付きAPIで取得", async () =>
    {
        using var editor = fixture.AuthenticatedClient("audit-owner", "Editor");
        var created = await CreateDraftAsync(editor, "auditor boundary");

        using var reviewer = fixture.AuthenticatedClient("audit-reviewer", "Reviewer");
        using var reviewerResponse = await reviewer.GetAsync("/api/ops/audit?limit=50");
        SpecAssert.Equal(HttpStatusCode.Forbidden, reviewerResponse.StatusCode, "Reviewer membership alone must not read tenant audit records.");

        using var auditor = fixture.AuthenticatedClient("tenant-auditor", "Auditor");
        using var auditResponse = await auditor.GetAsync($"/api/ops/audit/questions/{created.Id}?limit=50");
        SpecAssert.Equal(HttpStatusCode.OK, auditResponse.StatusCode, "Auditor must read bounded audit metadata for the current tenant.");
        var audit = await auditResponse.Content.ReadFromJsonAsync<JsonElement>();
        SpecAssert.True(
            audit.ValueKind == JsonValueKind.Array
            && audit.EnumerateArray().Any(record => record.GetProperty("targetId").GetGuid() == created.Id),
            "The question audit route must return the requested target metadata.");

        using var otherTenantAuditor = fixture.AuthenticatedClient(
            "other-tenant-auditor",
            "Auditor",
            externalOrganizationId: "org-other");
        using var otherTenantResponse = await otherTenantAuditor.GetAsync($"/api/ops/audit/questions/{created.Id}?limit=50");
        var otherTenantAudit = await otherTenantResponse.Content.ReadFromJsonAsync<JsonElement>();
        SpecAssert.Equal(HttpStatusCode.OK, otherTenantResponse.StatusCode, "A mapped Auditor may query only their own tenant boundary.");
        SpecAssert.Equal(0, otherTenantAudit.GetArrayLength(), "Another tenant must not observe the target audit metadata.");

        using var zeroLimit = await auditor.GetAsync("/api/ops/audit?limit=0");
        using var excessiveLimit = await auditor.GetAsync("/api/ops/audit?limit=201");
        SpecAssert.Equal(HttpStatusCode.BadRequest, zeroLimit.StatusCode, "Audit limit must be positive.");
        SpecAssert.Equal(HttpStatusCode.BadRequest, excessiveLimit.StatusCode, "Audit limit must not exceed 200.");

        using var retiredRoute = await auditor.GetAsync("/api/admin/audit");
        SpecAssert.Equal(HttpStatusCode.NotFound, retiredRoute.StatusCode, "The unbounded legacy audit route must be removed.");
    }),
    new("TC-ACC-MVS01-059", "REQ-MVS01-SEC-006", "過大な審査理由をAPI境界で拒否", async () =>
    {
        using var editor = fixture.AuthenticatedClient("reason-owner", "Editor");
        var created = await CreateDraftAsync(editor, "reason boundary");
        using var submitted = await editor.PostAsync($"/api/admin/questions/{created.Id}/submit", null);
        SpecAssert.Equal(HttpStatusCode.OK, submitted.StatusCode, "The reason-boundary precondition must submit.");
        using var reviewer = fixture.AuthenticatedClient("reason-reviewer", "Reviewer");
        using var response = await reviewer.PostAsJsonAsync(
            $"/api/admin/questions/{created.Id}/return",
            new ReviewReasonRequest(new string('x', 1001)));
        SpecAssert.Equal(HttpStatusCode.BadRequest, response.StatusCode, "An oversized review reason must be rejected before persistence.");
    }),
    new("TC-ACC-MVS01-021", "REQ-MVS01-SEC-001", "公開APIの過剰要求を429で抑止", async () =>
    {
        using var client = fixture.AnonymousClient();
        var rejected = false;
        for (var attempt = 0; attempt < 110; attempt++)
        {
            using var response = await client.GetAsync($"/api/public/questions/{Guid.NewGuid()}");
            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                rejected = true;
                break;
            }
        }

        SpecAssert.True(rejected, "The fixed-window limiter must eventually return 429.");
    }),
    new("TC-ACC-MVS01-070-API", "ADR-0009-D5,ADR-0009-D6", "相関IDと要求IDを分離して安全に伝播", async () =>
    {
        using var client = fixture.AnonymousClient();
        using var firstRequest = new HttpRequestMessage(
            HttpMethod.Get,
            $"/api/public/questions/{Guid.NewGuid()}");
        firstRequest.Headers.Add("X-Correlation-ID", "client-flow-123");
        using var first = await client.SendAsync(firstRequest);
        using var secondRequest = new HttpRequestMessage(
            HttpMethod.Get,
            $"/api/public/questions/{Guid.NewGuid()}");
        secondRequest.Headers.Add("X-Correlation-ID", "client-flow-123");
        using var second = await client.SendAsync(secondRequest);

        var firstCorrelation = Header(first, "X-Correlation-ID");
        var firstRequestId = Header(first, "X-Request-ID");
        var secondRequestId = Header(second, "X-Request-ID");
        SpecAssert.Equal("client-flow-123", firstCorrelation, "A safe caller correlation ID must be preserved.");
        SpecAssert.True(firstRequestId.Length is > 0 and <= 64, "Every response must expose one bounded request ID.");
        SpecAssert.False(firstRequestId == firstCorrelation, "Request ID and correlation ID must remain different concepts.");
        SpecAssert.False(firstRequestId == secondRequestId, "Every request must receive a fresh request ID.");
    }),
    new("TC-ACC-MVS01-071-API", "ADR-0009-D1,ADR-0010-D2", "拒否監査をPlatformAuditor専用経路へ集約し429を抑制", async () =>
    {
        await using var auditFixture = await ApiFixture.StartAsync(
            configuration:
            [
                "PublicRateLimit:PermitLimit=1",
                "Audit:PartitionHashKey=MDEyMzQ1Njc4OWFiY2RlZjAxMjM0NTY3ODlhYmNkZWY="
            ]);
        using var anonymous = auditFixture.AnonymousClient();
        for (var attempt = 0; attempt < 8; attempt++)
        {
            using var response = await anonymous.GetAsync($"/api/public/questions/{Guid.NewGuid()}");
        }

        using var tenantAuditor = auditFixture.AuthenticatedClient("tenant-auditor-denied", "Auditor");
        using var tenantResponse = await tenantAuditor.GetAsync(
            $"/api/platform/security-events?from={Uri.EscapeDataString(DateTimeOffset.UtcNow.AddMinutes(-5).ToString("O"))}&to={Uri.EscapeDataString(DateTimeOffset.UtcNow.AddMinutes(1).ToString("O"))}&limit=50");
        SpecAssert.Equal(HttpStatusCode.Forbidden, tenantResponse.StatusCode, "Tenant Auditor must not read platform security events.");

        using var platformAuditor = auditFixture.AuthenticatedClient(
            "platform-auditor",
            "PlatformAuditor",
            externalOrganizationId: null,
            verifiedIssuer: null);
        using var tenantAuditDenied = await platformAuditor.GetAsync("/api/ops/audit?limit=50");
        SpecAssert.Equal(HttpStatusCode.Forbidden, tenantAuditDenied.StatusCode, "PlatformAuditor must not inherit tenant Auditor access.");
        using var missingPeriod = await platformAuditor.GetAsync("/api/platform/security-events?limit=50");
        SpecAssert.Equal(HttpStatusCode.BadRequest, missingPeriod.StatusCode, "Platform audit queries must require an explicit period.");

        var from = Uri.EscapeDataString(DateTimeOffset.UtcNow.AddMinutes(-5).ToString("O"));
        var to = Uri.EscapeDataString(DateTimeOffset.UtcNow.AddMinutes(1).ToString("O"));
        var wire = string.Empty;
        for (var attempt = 0; attempt < 100; attempt++)
        {
            using var platformResponse = await platformAuditor.GetAsync(
                $"/api/platform/security-events?from={from}&to={to}&limit=50");
            SpecAssert.Equal(HttpStatusCode.OK, platformResponse.StatusCode, "PlatformAuditor must read only the bounded platform audit projection.");
            wire = await platformResponse.Content.ReadAsStringAsync();
            if (wire.Contains("access.rate_limited", StringComparison.Ordinal))
            {
                break;
            }

            await Task.Delay(20);
        }
        SpecAssert.True(wire.Contains("access.rate_limited", StringComparison.Ordinal), "The first 429 in the minute window must be recorded.");
        SpecAssert.False(wire.Contains("partitionHash", StringComparison.OrdinalIgnoreCase), "The partition hash must not be exposed by the API.");

        var metrics = auditFixture.AuditMetrics.Snapshot();
        SpecAssert.True(metrics.SecurityAuditSuppressedTotal >= 1, "Repeated 429 events must increment security_audit_suppressed_total.");
    }),
    new("TC-ACC-MVS01-080-API", "ADR-0009-D8", "監査sink障害と遅延でも元の拒否応答を維持", async () =>
    {
        await using var failureFixture = await ApiFixture.StartAsync(
            configuration:
            [
                "PublicRateLimit:PermitLimit=1",
                "Audit:WriteTimeoutMilliseconds=50",
                "Audit:PartitionHashKey=MDEyMzQ1Njc4OWFiY2RlZjAxMjM0NTY3ODlhYmNkZWY="
            ],
            configureServices: services => services.AddSingleton<IAuditSink, DelayedFailingAuditSink>());
        using var client = failureFixture.AnonymousClient();
        using var permitted = await client.GetAsync($"/api/public/questions/{Guid.NewGuid()}");

        var stopwatch = Stopwatch.StartNew();
        using var rejected = await client.GetAsync($"/api/public/questions/{Guid.NewGuid()}");
        stopwatch.Stop();
        SpecAssert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode, "Audit failure must not replace the original 429 response.");
        SpecAssert.True(stopwatch.Elapsed < TimeSpan.FromMilliseconds(500), "Audit I/O must not delay the rejection response path.");

        var observedFailure = await WaitUntilAsync(
            () => failureFixture.AuditMetrics.Snapshot().AuditWriteFailuresTotal >= 1,
            TimeSpan.FromSeconds(2));
        SpecAssert.True(observedFailure, "Audit failures must increment audit_write_failures_total for fallback monitoring.");
    })
};

return await SpecTestRunner.RunAsync("ToiNoMori.Api specification tests", tests);

static WebApplicationOptions ProductionOptions(string[] args) => new()
{
    Args = args,
    EnvironmentName = "Production",
    ApplicationName = typeof(AppHost).Assembly.FullName,
    ContentRootPath = AppContext.BaseDirectory
};

static QuestionContentRequest ValidContent(string suffix) =>
    new($"question {suffix}", $"body {suffix}", ["cloud", "library"]);

static string Header(HttpResponseMessage response, string name) =>
    response.Headers.TryGetValues(name, out var values)
        ? values.Single()
        : throw new TestFailureException($"Response header was missing: {name}");

static async Task<bool> WaitUntilAsync(Func<bool> predicate, TimeSpan timeout)
{
    var started = Stopwatch.StartNew();
    while (started.Elapsed < timeout)
    {
        if (predicate())
        {
            return true;
        }

        await Task.Delay(20);
    }

    return predicate();
}

static async Task<QuestionResponse> CreateDraftAsync(HttpClient client, string suffix)
{
    var response = await client.PostAsJsonAsync("/api/admin/questions", ValidContent(suffix));
    SpecAssert.Equal(HttpStatusCode.Created, response.StatusCode, "Test precondition create must succeed.");
    return await ReadQuestionAsync(response);
}

static async Task<QuestionResponse> ReadQuestionAsync(HttpResponseMessage response)
{
    return await response.Content.ReadFromJsonAsync<QuestionResponse>(QuestionJsonOptions())
        ?? throw new TestFailureException("Question response JSON was empty.");
}

static async Task<QuestionResponse[]> ReadQuestionsAsync(HttpResponseMessage response) =>
    await response.Content.ReadFromJsonAsync<QuestionResponse[]>(QuestionJsonOptions())
    ?? throw new TestFailureException("Question list response JSON was empty.");

static JsonSerializerOptions QuestionJsonOptions()
{
    var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
    options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseUpper));
    return options;
}

static Task<HttpResponseMessage> PutAsync(
    HttpClient client,
    Guid id,
    int version,
    QuestionContentRequest content)
{
    var request = new HttpRequestMessage(HttpMethod.Put, $"/api/admin/questions/{id}")
    {
        Content = JsonContent.Create(content)
    };
    request.Headers.TryAddWithoutValidation("If-Match", $"\"{version}\"");
    return client.SendAsync(request);
}

static Task<HttpResponseMessage> ApproveAsync(
    HttpClient client,
    Guid id,
    string idempotencyKey,
    int expectedVersion) => ApproveWithIfMatchAsync(
        client,
        id,
        idempotencyKey,
        $"\"{expectedVersion}\"");

static Task<HttpResponseMessage> ApproveWithIfMatchAsync(
    HttpClient client,
    Guid id,
    string idempotencyKey,
    string? ifMatch)
{
    var request = new HttpRequestMessage(HttpMethod.Post, $"/api/admin/questions/{id}/approve")
    {
        Content = new StringContent(string.Empty, Encoding.UTF8, "application/json")
    };
    request.Headers.Add("Idempotency-Key", idempotencyKey);
    if (ifMatch is not null)
    {
        request.Headers.TryAddWithoutValidation("If-Match", ifMatch);
    }

    return client.SendAsync(request);
}

static async Task<QuestionResponse> PublishAsync(
    ApiFixture fixture,
    string title,
    string ownerSubject,
    string reviewerSubject)
{
    using var editor = fixture.AuthenticatedClient(ownerSubject, "Editor");
    var created = await CreateDraftAsync(editor, title);
    var submitted = await editor.PostAsync($"/api/admin/questions/{created.Id}/submit", null);
    SpecAssert.Equal(HttpStatusCode.OK, submitted.StatusCode, "Test precondition submit must succeed.");
    using var reviewer = fixture.AuthenticatedClient(reviewerSubject, "Reviewer");
    var approved = await ApproveAsync(
        reviewer,
        created.Id,
        $"approve-{created.Id}",
        expectedVersion: created.Version + 1);
    SpecAssert.Equal(HttpStatusCode.OK, approved.StatusCode, "Test precondition approve must succeed.");
    return await ReadQuestionAsync(approved);
}

file sealed class TemporaryDataProtectionMaterial : IDisposable
{
    private TemporaryDataProtectionMaterial(
        string rootPath,
        string keyRingPath,
        string certificatePath,
        string certificatePassword)
    {
        RootPath = rootPath;
        KeyRingPath = keyRingPath;
        CertificatePath = certificatePath;
        CertificatePassword = certificatePassword;
    }

    private string RootPath { get; }

    public string KeyRingPath { get; }

    public string CertificatePath { get; }

    public string CertificatePassword { get; }

    public static TemporaryDataProtectionMaterial Create()
    {
        var rootPath = Path.Combine(Path.GetTempPath(), $"toi-no-mori-dp-test.{Guid.NewGuid():N}");
        var keyRingPath = Path.Combine(rootPath, "key-ring");
        Directory.CreateDirectory(keyRingPath);
        const string password = "test-only-pfx-password";
        var certificatePath = Path.Combine(rootPath, "key-protection.pfx");

        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest(
            "CN=ToiNoMori Test Data Protection",
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(
            X509KeyUsageFlags.KeyEncipherment | X509KeyUsageFlags.DataEncipherment,
            true));
        using var certificate = request.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddMinutes(-1),
            DateTimeOffset.UtcNow.AddDays(1));
        File.WriteAllBytes(certificatePath, certificate.Export(X509ContentType.Pfx, password));

        return new(rootPath, keyRingPath, certificatePath, password);
    }

    public void Dispose()
    {
        if (RootPath.StartsWith(Path.GetTempPath(), StringComparison.Ordinal)
            && Directory.Exists(RootPath))
        {
            Directory.Delete(RootPath, recursive: true);
        }
    }
}

file sealed class DelayedFailingAuditSink : IAuditSink
{
    public async Task<AuditOutcomeRecorded> WriteAsync(
        AccessDenialAuditEnvelope envelope,
        CancellationToken cancellationToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
        throw new InvalidOperationException("test-only audit sink failure");
    }
}
