using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using ToiNoMori.Api;
using ToiNoMori.Testing;

var projectRoot = Directory.GetCurrentDirectory();
var webRoot = Path.Combine(projectRoot, "src", "ToiNoMori.Api", "wwwroot", "app");
var html = await File.ReadAllTextAsync(Path.Combine(webRoot, "index.html"));
var css = await File.ReadAllTextAsync(Path.Combine(webRoot, "styles.css"));
var javascript = await File.ReadAllTextAsync(Path.Combine(webRoot, "app.js"));
var manifest = await File.ReadAllTextAsync(Path.Combine(webRoot, "manifest.webmanifest"));
string[] labeledControls = ["query", "tag", "question-title", "question-body", "question-tags"];
string[] forbiddenBrowserApis =
[
    "innerHTML", "outerHTML", "insertAdjacentHTML", "document.write", "localStorage", "sessionStorage", "eval("
];

var tests = new List<SpecTest>
{
    new("TC-ACC-MVS01-039", "REQ-MVS01-MOB-001", "360px幅と44pxタッチ操作へ応答", () =>
    {
        SpecAssert.True(
            html.Contains("width=device-width, initial-scale=1, viewport-fit=cover", StringComparison.Ordinal),
            "The mobile shell must declare a device-width viewport.");
        SpecAssert.True(css.Contains("@media (max-width: 30rem)", StringComparison.Ordinal), "CSS must define a narrow-screen layout.");
        SpecAssert.True(css.Contains("grid-template-columns: 1fr", StringComparison.Ordinal), "Narrow layouts must collapse to one column.");
        SpecAssert.True(css.Contains("min-block-size: 2.75rem", StringComparison.Ordinal), "Interactive controls must be at least 44 CSS pixels high.");
        SpecAssert.True(css.Contains("env(safe-area-inset-bottom)", StringComparison.Ordinal), "The layout must respect mobile safe areas.");
        SpecAssert.True(css.Contains("prefers-reduced-motion: reduce", StringComparison.Ordinal), "Motion must respect user preferences.");
        return Task.CompletedTask;
    }),
    new("TC-ACC-MVS01-040", "REQ-MVS01-MOB-002", "キーボードと支援技術向け構造を提供", () =>
    {
        SpecAssert.True(html.Contains("class=\"skip-link\"", StringComparison.Ordinal), "A keyboard skip link is required.");
        SpecAssert.True(html.Contains("<main id=\"main-content\">", StringComparison.Ordinal), "The page must expose a main landmark.");
        SpecAssert.True(html.Contains("role=\"search\"", StringComparison.Ordinal), "The search form must expose its role.");
        SpecAssert.True(html.Contains("aria-live=\"polite\"", StringComparison.Ordinal), "Asynchronous status must be announced.");
        foreach (var control in labeledControls)
        {
            SpecAssert.True(html.Contains($"for=\"{control}\"", StringComparison.Ordinal), $"Control {control} must have a label.");
        }
        SpecAssert.True(css.Contains(":focus-visible", StringComparison.Ordinal), "Keyboard focus must be visible.");
        return Task.CompletedTask;
    }),
    new("TC-ACC-MVS01-041", "REQ-MVS01-SEC-004", "ブラウザへtokenを保存せず安全に文字列を描画", () =>
    {
        foreach (var forbidden in forbiddenBrowserApis)
        {
            SpecAssert.False(javascript.Contains(forbidden, StringComparison.Ordinal), $"Frontend must not use {forbidden}.");
        }
        SpecAssert.True(javascript.Contains("textContent", StringComparison.Ordinal), "Untrusted text must use textContent.");
        SpecAssert.True(javascript.Contains("credentials: \"same-origin\"", StringComparison.Ordinal), "Requests must remain same-origin.");
        SpecAssert.False(html.Contains("http://", StringComparison.OrdinalIgnoreCase), "HTML must not load insecure external resources.");
        SpecAssert.False(html.Contains("https://", StringComparison.OrdinalIgnoreCase), "HTML must not load third-party resources.");
        SpecAssert.False(html.Contains("onclick=", StringComparison.OrdinalIgnoreCase), "Inline event handlers are forbidden.");
        SpecAssert.False(html.Contains("<style", StringComparison.OrdinalIgnoreCase), "Inline styles are forbidden.");
        return Task.CompletedTask;
    }),
    new("TC-ACC-MVS01-042", "REQ-MVS01-SEC-004", "同一オリジン静的UIをCSPとno-storeで配信", async () =>
    {
        await using var app = AppHost.Build(new WebApplicationOptions
        {
            Args =
            [
                "Logging:LogLevel:Default=Warning",
                "Logging:LogLevel:Microsoft.AspNetCore.DataProtection=Error"
            ],
            EnvironmentName = "Testing",
            ApplicationName = typeof(AppHost).Assembly.FullName,
            ContentRootPath = Path.Combine(projectRoot, "src", "ToiNoMori.Api")
        });
        app.Urls.Add("http://127.0.0.1:0");
        await app.StartAsync();
        var address = app.Services
            .GetRequiredService<IServer>()
            .Features
            .Get<IServerAddressesFeature>()
            ?.Addresses
            .SingleOrDefault()
            ?? throw new TestFailureException("Kestrel did not publish a mobile test address.");
        using var client = new HttpClient { BaseAddress = new Uri(address) };

        using var indexResponse = await client.GetAsync("/app/");
        using var cssResponse = await client.GetAsync("/app/styles.css");
        using var scriptResponse = await client.GetAsync("/app/app.js");
        using var manifestResponse = await client.GetAsync("/app/manifest.webmanifest");

        SpecAssert.Equal(HttpStatusCode.OK, indexResponse.StatusCode, "Mobile shell must be served.");
        SpecAssert.Equal("text/html", indexResponse.Content.Headers.ContentType?.MediaType, "Shell must be HTML.");
        SpecAssert.Equal("text/css", cssResponse.Content.Headers.ContentType?.MediaType, "Stylesheet must have a CSS media type.");
        SpecAssert.True(
            scriptResponse.Content.Headers.ContentType?.MediaType is "text/javascript" or "application/javascript",
            "Script must have a JavaScript media type.");
        SpecAssert.Equal(HttpStatusCode.OK, manifestResponse.StatusCode, "Web manifest must be served.");
        SpecAssert.True(indexResponse.Headers.CacheControl?.NoStore == true, "The app shell must not be cached.");
        var csp = indexResponse.Headers.GetValues("Content-Security-Policy").Single();
        SpecAssert.True(csp.Contains("script-src 'self'", StringComparison.Ordinal), "CSP must restrict scripts to self.");
        SpecAssert.False(csp.Contains("'unsafe-inline'", StringComparison.Ordinal), "CSP must forbid inline script and style execution.");

        var manifestJson = JsonDocument.Parse(manifest).RootElement;
        SpecAssert.Equal("/app/", manifestJson.GetProperty("start_url").GetString(), "Manifest must start inside the app scope.");
    }),
    new("TC-ACC-MVS01-055", "REQ-MVS01-UI-001/002", "編集・審査・監査画面を権限別に提供", () =>
    {
        string[] requiredViews = ["editor-view", "reviewer-view", "audit-view"];
        foreach (var view in requiredViews)
        {
            SpecAssert.True(html.Contains($"id=\"{view}\"", StringComparison.Ordinal), $"Workspace view {view} must exist.");
            SpecAssert.True(javascript.Contains($"\"{view}\"", StringComparison.Ordinal), $"Workspace view {view} must be controlled by script.");
        }
        SpecAssert.True(html.Contains("id=\"editor-question-list\"", StringComparison.Ordinal), "The Editor must have a managed-question list.");
        SpecAssert.True(html.Contains("id=\"review-queue-list\"", StringComparison.Ordinal), "The Reviewer must have a review queue.");
        SpecAssert.True(html.Contains("id=\"published-question-list\"", StringComparison.Ordinal), "The Reviewer must see published questions.");
        SpecAssert.True(javascript.Contains("Auditor", StringComparison.Ordinal), "Only an Auditor may be offered the audit view.");
        SpecAssert.False(
            javascript.Contains("hasRole(\"Reviewer\") && showView(\"audit-view\")", StringComparison.Ordinal),
            "Reviewer membership alone must not expose the audit view.");
        SpecAssert.True(css.Contains(".management-list", StringComparison.Ordinal), "Management cards must have a mobile layout.");
        SpecAssert.True(css.Contains(".workspace-nav", StringComparison.Ordinal), "Role views must have touch navigation.");
        return Task.CompletedTask;
    }),
    new("TC-ACC-MVS01-056", "REQ-MVS01-SEC-006", "画面更新をCSRF・版・冪等キーで保護", () =>
    {
        SpecAssert.True(javascript.Contains("X-CSRF-Token", StringComparison.Ordinal), "Every browser mutation must carry the BFF CSRF token.");
        SpecAssert.True(javascript.Contains("If-Match", StringComparison.Ordinal), "Editor updates must carry the displayed version.");
        SpecAssert.True(javascript.Contains("Idempotency-Key", StringComparison.Ordinal), "Approval must carry an idempotency key.");
        SpecAssert.True(javascript.Contains("crypto.randomUUID()", StringComparison.Ordinal), "Approval keys must use browser cryptographic randomness.");
        SpecAssert.True(javascript.Contains("question.ownerSubject === state.session.subject", StringComparison.Ordinal), "The UI must identify self-approval before submission.");
        return Task.CompletedTask;
    })
};

return await SpecTestRunner.RunAsync("ToiNoMori mobile web specification tests", tests);
