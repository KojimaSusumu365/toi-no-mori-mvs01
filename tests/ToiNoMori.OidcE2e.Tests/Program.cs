using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ToiNoMori.OidcE2e.Tests;
using ToiNoMori.Testing;

string[] oidcTags = ["oidc"];
string[] stage6Tags = ["oidc", "stage6"];
string[] stage6r9Tags = ["oidc", "tenant"];
var tests = new List<SpecTest>
{
    new("TC-ACC-MVS01-043", "REQ-MVS01-IAM-004", "署名付きOIDC code+PKCEを実HTTPSで往復", async () =>
    {
        await using var fixture = await OidcE2eFixture.StartAsync(TestIdentityProfile.EditorWithMfa);
        using var login = await fixture.LoginAsync();
        SpecAssert.Equal(HttpStatusCode.OK, login.StatusCode, "OIDC login must return to the app shell.");
        SpecAssert.Equal("/app/", login.RequestMessage?.RequestUri?.AbsolutePath, "Login must return inside the app.");
        SpecAssert.Equal(1, fixture.IdentityProvider.AuthorizationRequestCount, "The authorization endpoint must be called once.");
        SpecAssert.Equal(1, fixture.IdentityProvider.TokenRequestCount, "The authorization code must be redeemed once.");
        SpecAssert.True(fixture.IdentityProvider.PkceVerified, "The identity provider must verify the PKCE verifier.");
        SpecAssert.True(fixture.IdentityProvider.ClientAuthenticationVerified, "The token endpoint must authenticate the confidential client.");
        SpecAssert.True(fixture.IdentityProvider.NonceReturned, "The signed ID token must bind the browser nonce.");

        using var session = await fixture.GetSessionAsync();
        var wire = await session.Content.ReadAsStringAsync();
        var payload = JsonDocument.Parse(wire).RootElement;
        SpecAssert.Equal(HttpStatusCode.OK, session.StatusCode, "A signed MFA session must be accepted.");
        SpecAssert.Equal("editor-e2e", payload.GetProperty("subject").GetString(), "The validated subject must reach the BFF session.");
        SpecAssert.True(
            payload.GetProperty("roles").EnumerateArray().Any(role => role.GetString() == "Editor"),
            "The validated Editor role must reach the session.");
        SpecAssert.False(wire.Contains(TestOidcProvider.AccessTokenSentinel, StringComparison.Ordinal), "The access token must remain server-side.");

        var cookie = fixture.SessionCookie;
        SpecAssert.NotNull(cookie, "A BFF session cookie must be issued.");
        SpecAssert.True(cookie!.HttpOnly, "The BFF session cookie must be HttpOnly.");
        SpecAssert.True(cookie.Secure, "The BFF session cookie must be Secure.");
        SpecAssert.False(
            fixture.Cookies.Any(item => item.Value.Contains(TestOidcProvider.AccessTokenSentinel, StringComparison.Ordinal)),
            "No browser cookie may contain the access token.");
    }),
    new("TC-ACC-MVS01-044", "REQ-MVS01-IAM-003", "署名が正しくてもMFA証跡なしを403", async () =>
    {
        await using var fixture = await OidcE2eFixture.StartAsync(TestIdentityProfile.EditorWithoutMfa);
        using var login = await fixture.LoginAsync();
        SpecAssert.Equal(HttpStatusCode.OK, login.StatusCode, "The identity can sign in before authorization is evaluated.");
        SpecAssert.True(fixture.IdentityProvider.PkceVerified, "The non-MFA token must still come through a valid OIDC flow.");
        using var session = await fixture.GetSessionAsync();
        SpecAssert.Equal(HttpStatusCode.Forbidden, session.StatusCode, "A signed token without MFA evidence must not open an administrative session.");
    }),
    new("TC-ACC-MVS01-045", "REQ-MVS01-SEC-005", "未登録鍵で署名されたID tokenを拒否", async () =>
    {
        await using var fixture = await OidcE2eFixture.StartAsync(TestIdentityProfile.InvalidSignature);
        using var login = await fixture.LoginAsync();
        SpecAssert.Equal(HttpStatusCode.OK, login.StatusCode, "A remote authentication failure must return a safe app page.");
        SpecAssert.Equal("failed", GetQueryValue(login.RequestMessage?.RequestUri, "authentication"), "The UI must receive only a generic failure marker.");
        using var session = await fixture.GetSessionAsync();
        SpecAssert.Equal(HttpStatusCode.Unauthorized, session.StatusCode, "An invalid signature must not create a session.");
        SpecAssert.True(fixture.SessionCookie is null, "An invalid signature must not issue a BFF session cookie.");
    }),
    new("TC-ACC-MVS01-046", "REQ-MVS01-SEC-003", "実OIDC sessionの更新をCSRFで保護", async () =>
    {
        await using var fixture = await OidcE2eFixture.StartAsync(TestIdentityProfile.EditorWithMfa);
        using var login = await fixture.LoginAsync();
        SpecAssert.Equal(HttpStatusCode.OK, login.StatusCode, "OIDC login must succeed before a write.");
        var csrfToken = await fixture.ReadCsrfTokenAsync();
        var content = new { title = "OIDC E2E question", body = "Created through a signed browser session.", tags = oidcTags };

        using var rejected = await fixture.Browser.PostAsJsonAsync("/api/admin/questions", content);
        SpecAssert.Equal(HttpStatusCode.Forbidden, rejected.StatusCode, "A Cookie-authenticated write without CSRF must be rejected.");

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/admin/questions")
        {
            Content = JsonContent.Create(content)
        };
        request.Headers.Add("X-CSRF-Token", csrfToken);
        using var accepted = await fixture.Browser.SendAsync(request);
        SpecAssert.Equal(HttpStatusCode.Created, accepted.StatusCode, "The MFA Editor session with its bound CSRF token may create a draft.");
    }),
    new("TC-ACC-MVS01-047", "REQ-MVS01-IAM-004", "CSRF付きlogoutでlocal/IdP sessionを終了", async () =>
    {
        await using var fixture = await OidcE2eFixture.StartAsync(TestIdentityProfile.EditorWithMfa);
        using var login = await fixture.LoginAsync();
        SpecAssert.Equal(HttpStatusCode.OK, login.StatusCode, "OIDC login must succeed before logout.");
        var csrfToken = await fixture.ReadCsrfTokenAsync();
        using var logout = await fixture.Browser.PostAsync(
            "/bff/logout?returnUrl=/app/",
            new FormUrlEncodedContent(new Dictionary<string, string> { ["csrfToken"] = csrfToken }));
        SpecAssert.Equal(HttpStatusCode.OK, logout.StatusCode, "Logout must return to the app shell.");
        SpecAssert.Equal(1, fixture.IdentityProvider.EndSessionRequestCount, "The BFF must call the identity provider end-session endpoint.");
        using var session = await fixture.GetSessionAsync();
        SpecAssert.Equal(HttpStatusCode.Unauthorized, session.StatusCode, "The local BFF session must be gone after logout.");
        SpecAssert.True(fixture.SessionCookie is null, "The browser session cookie must be removed after logout.");
    }),
    new("TC-ACC-MVS01-048", "REQ-MVS01-IAM-004", "15分を超える古い認証を拒否", async () =>
    {
        await using var fixture = await OidcE2eFixture.StartAsync(TestIdentityProfile.StaleAuthentication);
        using var login = await fixture.LoginAsync();
        SpecAssert.Equal(HttpStatusCode.OK, login.StatusCode, "A stale authentication failure must return a safe app page.");
        SpecAssert.Equal("failed", GetQueryValue(login.RequestMessage?.RequestUri, "authentication"), "The UI must receive only a generic failure marker.");
        using var session = await fixture.GetSessionAsync();
        SpecAssert.Equal(HttpStatusCode.Unauthorized, session.StatusCode, "Authentication older than max_age must not create a session.");
        SpecAssert.True(fixture.SessionCookie is null, "A stale authentication must not issue a BFF session cookie.");
    }),
    new("TC-ACC-MVS01-057", "REQ-MVS01-UI-003", "別OIDC利用者で作成・申請・承認・公開を完結", async () =>
    {
        await using var fixture = await OidcE2eFixture.StartAsync(TestIdentityProfile.EditorWithMfa);
        using var editorLogin = await fixture.LoginAsync();
        SpecAssert.Equal(HttpStatusCode.OK, editorLogin.StatusCode, "The Editor OIDC login must succeed.");
        var editorCsrf = await fixture.ReadCsrfTokenAsync();

        using var createRequest = new HttpRequestMessage(HttpMethod.Post, "/api/admin/questions")
        {
            Content = JsonContent.Create(new
            {
                title = "OIDC Stage 6 workflow",
                body = "Editor and Reviewer use distinct signed sessions.",
                tags = stage6Tags
            })
        };
        createRequest.Headers.Add("X-CSRF-Token", editorCsrf);
        using var createdResponse = await fixture.Browser.SendAsync(createRequest);
        SpecAssert.Equal(HttpStatusCode.Created, createdResponse.StatusCode, "The signed Editor session must create a draft.");
        var created = await createdResponse.Content.ReadFromJsonAsync<JsonElement>();
        var questionId = created.GetProperty("id").GetGuid();

        using var submitRequest = new HttpRequestMessage(HttpMethod.Post, $"/api/admin/questions/{questionId}/submit");
        submitRequest.Headers.Add("X-CSRF-Token", editorCsrf);
        using var submitted = await fixture.Browser.SendAsync(submitRequest);
        SpecAssert.Equal(HttpStatusCode.OK, submitted.StatusCode, "The Editor session must submit the draft.");

        using var reviewerBrowser = fixture.CreateBrowser();
        using var reviewerLogin = await fixture.LoginAsync(reviewerBrowser, TestIdentityProfile.ReviewerWithMfa);
        SpecAssert.Equal(HttpStatusCode.OK, reviewerLogin.StatusCode, "The distinct Reviewer OIDC login must succeed.");
        var reviewerCsrf = await OidcE2eFixture.ReadCsrfTokenAsync(reviewerBrowser);
        using var queueResponse = await reviewerBrowser.GetAsync("/api/admin/questions?status=IN_REVIEW");
        var queue = await queueResponse.Content.ReadFromJsonAsync<JsonElement>();
        var queuedQuestion = queue.EnumerateArray()
            .SingleOrDefault(item => item.GetProperty("id").GetGuid() == questionId);
        SpecAssert.True(queuedQuestion.ValueKind == JsonValueKind.Object, "The Reviewer session must see the submitted question.");
        var reviewedVersion = queuedQuestion.GetProperty("version").GetInt32();

        using var approveRequest = new HttpRequestMessage(HttpMethod.Post, $"/api/admin/questions/{questionId}/approve")
        {
            Content = JsonContent.Create(new { })
        };
        approveRequest.Headers.Add("X-CSRF-Token", reviewerCsrf);
        approveRequest.Headers.Add("Idempotency-Key", $"oidc-stage6-{questionId}");
        approveRequest.Headers.TryAddWithoutValidation("If-Match", $"\"{reviewedVersion}\"");
        using var approved = await reviewerBrowser.SendAsync(approveRequest);
        SpecAssert.Equal(HttpStatusCode.OK, approved.StatusCode, "The distinct Reviewer session must approve the question.");

        using var anonymousBrowser = fixture.CreateBrowser();
        using var published = await anonymousBrowser.GetAsync($"/api/public/questions/{questionId}");
        SpecAssert.Equal(HttpStatusCode.OK, published.StatusCode, "The approved question must be visible without a session.");
        SpecAssert.Equal(2, fixture.IdentityProvider.AuthorizationRequestCount, "Two distinct OIDC browser logins must be used.");
    }),
    new("TC-ACC-MVS01-077-OIDC", "ADR-0007-D2,ADR-0008-D1", "実OIDC tenant mappingとdual-role自己承認拒否", async () =>
    {
        await using var fixture = await OidcE2eFixture.StartAsync(TestIdentityProfile.EditorReviewerWithMfa);
        using var ownerLogin = await fixture.LoginAsync();
        SpecAssert.Equal(HttpStatusCode.OK, ownerLogin.StatusCode, "The mapped dual-role OIDC identity must sign in.");
        var ownerCsrf = await fixture.ReadCsrfTokenAsync();

        using var createRequest = new HttpRequestMessage(HttpMethod.Post, "/api/admin/questions")
        {
            Content = JsonContent.Create(new
            {
                title = "OIDC tenant mapping boundary",
                body = "A mapped dual-role subject must still fail self approval.",
                tags = stage6r9Tags
            })
        };
        createRequest.Headers.Add("X-CSRF-Token", ownerCsrf);
        using var createdResponse = await fixture.Browser.SendAsync(createRequest);
        SpecAssert.Equal(HttpStatusCode.Created, createdResponse.StatusCode, "The mapped internal tenant must permit creation.");
        var created = await createdResponse.Content.ReadFromJsonAsync<JsonElement>();
        var questionId = created.GetProperty("id").GetGuid();
        var stored = fixture.Store.FindAdministrative(
            ToiNoMori.Domain.TenantIds.Mvs01,
            questionId,
            "dual-role-owner-e2e",
            isReviewer: false);
        SpecAssert.NotNull(stored, "The signed external organization must map to the configured internal tenant UUID.");

        using var submitRequest = new HttpRequestMessage(HttpMethod.Post, $"/api/admin/questions/{questionId}/submit");
        submitRequest.Headers.Add("X-CSRF-Token", ownerCsrf);
        using var submitted = await fixture.Browser.SendAsync(submitRequest);
        SpecAssert.Equal(HttpStatusCode.OK, submitted.StatusCode, "The mapped owner must submit before the self-approval check.");

        using var ownerDetail = await fixture.Browser.GetAsync($"/api/admin/questions/{questionId}");
        var reviewedEtag = ownerDetail.Headers.ETag?.Tag;
        SpecAssert.Equal(HttpStatusCode.OK, ownerDetail.StatusCode, "The dual-role owner may review the mapped detail.");
        SpecAssert.NotNull(reviewedEtag, "The self-approval request must use the reviewed detail If-Match value.");

        using var selfApproval = new HttpRequestMessage(HttpMethod.Post, $"/api/admin/questions/{questionId}/approve")
        {
            Content = JsonContent.Create(new { })
        };
        selfApproval.Headers.Add("X-CSRF-Token", ownerCsrf);
        selfApproval.Headers.Add("Idempotency-Key", $"oidc-self-{questionId}");
        selfApproval.Headers.TryAddWithoutValidation("If-Match", reviewedEtag);
        using var selfApprovalResponse = await fixture.Browser.SendAsync(selfApproval);
        SpecAssert.Equal(HttpStatusCode.Forbidden, selfApprovalResponse.StatusCode, "Reviewer role must not bypass the self approval boundary.");

        using var reviewerBrowser = fixture.CreateBrowser();
        using var reviewerLogin = await fixture.LoginAsync(reviewerBrowser, TestIdentityProfile.ReviewerWithMfa);
        SpecAssert.Equal(HttpStatusCode.OK, reviewerLogin.StatusCode, "A distinct mapped Reviewer must sign in.");
        var reviewerCsrf = await OidcE2eFixture.ReadCsrfTokenAsync(reviewerBrowser);
        using var reviewerDetail = await reviewerBrowser.GetAsync($"/api/admin/questions/{questionId}");
        SpecAssert.Equal(HttpStatusCode.OK, reviewerDetail.StatusCode, "The same tenant mapping must make the question visible to a distinct Reviewer.");
        using var approval = new HttpRequestMessage(HttpMethod.Post, $"/api/admin/questions/{questionId}/approve")
        {
            Content = JsonContent.Create(new { })
        };
        approval.Headers.Add("X-CSRF-Token", reviewerCsrf);
        approval.Headers.Add("Idempotency-Key", $"oidc-distinct-{questionId}");
        approval.Headers.TryAddWithoutValidation("If-Match", reviewerDetail.Headers.ETag?.Tag);
        using var approved = await reviewerBrowser.SendAsync(approval);
        SpecAssert.Equal(HttpStatusCode.OK, approved.StatusCode, "A distinct mapped Reviewer may approve the question.");

        using var unmappedBrowser = fixture.CreateBrowser();
        using var unmappedLogin = await fixture.LoginAsync(
            unmappedBrowser,
            TestIdentityProfile.UnmappedOrganizationEditorWithMfa);
        SpecAssert.Equal("failed", GetQueryValue(unmappedLogin.RequestMessage?.RequestUri, "authentication"), "An unmapped signed organization must fail during OIDC tenant mapping.");
        using var unmappedSession = await unmappedBrowser.GetAsync("/bff/session");
        SpecAssert.Equal(HttpStatusCode.Unauthorized, unmappedSession.StatusCode, "An unmapped organization must not receive a BFF session.");
    })
};

return await SpecTestRunner.RunAsync("ToiNoMori OIDC browser protocol E2E tests", tests);

static string? GetQueryValue(Uri? uri, string name)
{
    if (uri is null)
    {
        return null;
    }

    var query = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(uri.Query);
    return query.TryGetValue(name, out var value) ? value.ToString() : null;
}
