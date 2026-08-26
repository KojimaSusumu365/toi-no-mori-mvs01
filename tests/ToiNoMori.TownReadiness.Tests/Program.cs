using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ToiNoMori.Api;
using ToiNoMori.Api.Tests;
using ToiNoMori.Domain;
using ToiNoMori.Testing;

await using var fixture = await ApiFixture.StartAsync();

var tests = new List<SpecTest>
{
    new("TC-ACC-MVS01-082-TR", "REQ-QF-TR-001", "Question IDを公開・撤回まで安定参照できる", async () =>
    {
        using var editor = fixture.AuthenticatedClient("tr-stable-editor", "Editor");
        var created = await CreateDraftAsync(editor, "stable-id");
        SpecAssert.True(created.Id != Guid.Empty, "Question must receive a non-empty stable identifier.");

        var submitted = await SubmitAsync(editor, created.Id);
        SpecAssert.Equal(created.Id, submitted.Id, "Submit must preserve the Question identifier.");
        SpecAssert.Equal(QuestionStatus.InReview, submitted.Status, "Submit must move the Question to IN_REVIEW.");

        using var reviewer = fixture.AuthenticatedClient("tr-stable-reviewer", "Reviewer");
        var published = await ApproveAsync(reviewer, submitted);
        SpecAssert.Equal(created.Id, published.Id, "Approve must preserve the Question identifier.");
        SpecAssert.Equal(QuestionStatus.Published, published.Status, "Approve must publish the Question.");

        using var anonymous = fixture.AnonymousClient();
        using var publicResponse = await anonymous.GetAsync($"/api/public/questions/{created.Id}");
        SpecAssert.Equal(HttpStatusCode.OK, publicResponse.StatusCode, "The same Question ID must resolve on the public boundary after publication.");
        var publicQuestion = await publicResponse.Content.ReadFromJsonAsync<PublicQuestionResponse>()
            ?? throw new TestFailureException("Public Question JSON was empty.");
        SpecAssert.Equal(created.Id, publicQuestion.Id, "The public projection must keep the original Question identifier.");

        var withdrawn = await WithdrawAsync(reviewer, created.Id, "town-readiness lifecycle check");
        SpecAssert.Equal(created.Id, withdrawn.Id, "Withdraw must preserve the Question identifier.");
        SpecAssert.Equal(QuestionStatus.Withdrawn, withdrawn.Status, "Withdraw must retain the stable ID while changing lifecycle state.");
    }),

    new("TC-ACC-MVS01-083-TR", "REQ-QF-TR-002", "公開Read境界はPUBLISHEDだけを外部へ返す", async () =>
    {
        var token = UniqueToken("trp");
        using var editor = fixture.AuthenticatedClient("tr-public-editor", "Editor");
        var created = await CreateDraftAsync(editor, token);
        using var anonymous = fixture.AnonymousClient();

        using (var draftDetail = await anonymous.GetAsync($"/api/public/questions/{created.Id}"))
        {
            SpecAssert.Equal(HttpStatusCode.NotFound, draftDetail.StatusCode, "DRAFT must not cross the public read boundary.");
        }
        var draftSearch = await SearchPublicAsync(anonymous, token);
        SpecAssert.False(draftSearch.Any(value => value.Id == created.Id), "DRAFT must not appear in public search.");

        var submitted = await SubmitAsync(editor, created.Id);
        using (var reviewDetail = await anonymous.GetAsync($"/api/public/questions/{created.Id}"))
        {
            SpecAssert.Equal(HttpStatusCode.NotFound, reviewDetail.StatusCode, "IN_REVIEW must not cross the public read boundary.");
        }

        using var reviewer = fixture.AuthenticatedClient("tr-public-reviewer", "Reviewer");
        var published = await ApproveAsync(reviewer, submitted);
        using (var publicDetail = await anonymous.GetAsync($"/api/public/questions/{published.Id}"))
        {
            SpecAssert.Equal(HttpStatusCode.OK, publicDetail.StatusCode, "PUBLISHED must be readable on the public boundary.");
        }
        var publishedSearch = await SearchPublicAsync(anonymous, token);
        SpecAssert.True(publishedSearch.Any(value => value.Id == published.Id), "PUBLISHED must appear in public search.");
    }),

    new("TC-ACC-MVS01-084-TR", "REQ-QF-TR-003", "公開DTOから内部審査・所有者・tenant情報を除外", async () =>
    {
        using var editor = fixture.AuthenticatedClient("tr-private-owner", "Editor");
        var created = await CreateDraftAsync(editor, "public-dto-minimum");
        var submitted = await SubmitAsync(editor, created.Id);
        using var reviewer = fixture.AuthenticatedClient("tr-private-reviewer", "Reviewer");
        var published = await ApproveAsync(reviewer, submitted);

        using var anonymous = fixture.AnonymousClient();
        using var response = await anonymous.GetAsync($"/api/public/questions/{published.Id}");
        SpecAssert.Equal(HttpStatusCode.OK, response.StatusCode, "Published Question must be readable before DTO inspection.");
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var properties = document.RootElement.EnumerateObject().Select(value => value.Name).OrderBy(value => value).ToArray();
        var expected = PublicQuestionFieldAllowlist.Names.OrderBy(value => value).ToArray();
        SpecAssert.True(properties.SequenceEqual(expected), "Public DTO must be an allowlist of id/title/body/tags/publishedAt only.");
        SpecAssert.False(document.RootElement.TryGetProperty("ownerSubject", out _), "Owner subject must never cross the public boundary.");
        SpecAssert.False(document.RootElement.TryGetProperty("reviewReason", out _), "Review reason must never cross the public boundary.");
        SpecAssert.False(document.RootElement.TryGetProperty("withdrawalReason", out _), "Withdrawal reason must never cross the public boundary.");
        SpecAssert.False(document.RootElement.TryGetProperty("tenantId", out _), "Tenant identity must never cross the public boundary.");
        SpecAssert.False(document.RootElement.TryGetProperty("approvedBy", out _), "Reviewer identity must never cross the public boundary.");
    }),

    new("TC-ACC-MVS01-085-TR", "REQ-QF-TR-004", "未知のTownAdmin roleからForest権限を自動導出しない", async () =>
    {
        using var editor = fixture.AuthenticatedClient("tr-role-editor", "Editor");
        var created = await CreateDraftAsync(editor, "role-independence");
        var submitted = await SubmitAsync(editor, created.Id);
        using var reviewer = fixture.AuthenticatedClient("tr-role-reviewer", "Reviewer");
        var published = await ApproveAsync(reviewer, submitted);

        using var townAdmin = fixture.AuthenticatedClient("future-town-admin", "TownAdmin");
        using (var adminRead = await townAdmin.GetAsync($"/api/admin/questions/{published.Id}"))
        {
            SpecAssert.Equal(HttpStatusCode.Forbidden, adminRead.StatusCode, "TownAdmin alone must not inherit Forest Staff access.");
        }
        using (var adminCreate = await townAdmin.PostAsJsonAsync("/api/admin/questions", ValidContent("town-role-create")))
        {
            SpecAssert.Equal(HttpStatusCode.Forbidden, adminCreate.StatusCode, "TownAdmin alone must not inherit Forest Editor access.");
        }
        using (var publicRead = await townAdmin.GetAsync($"/api/public/questions/{published.Id}"))
        {
            SpecAssert.Equal(HttpStatusCode.OK, publicRead.StatusCode, "A future Town identity may still use the role-neutral public read boundary.");
        }
    }),

    new("TC-ACC-MVS01-086-TR", "REQ-QF-TR-005", "公開停止後は同じ参照IDの本文をPublic APIから取得できない", async () =>
    {
        var token = UniqueToken("trw");
        using var editor = fixture.AuthenticatedClient("tr-withdraw-editor", "Editor");
        var created = await CreateDraftAsync(editor, token);
        var submitted = await SubmitAsync(editor, created.Id);
        using var reviewer = fixture.AuthenticatedClient("tr-withdraw-reviewer", "Reviewer");
        var published = await ApproveAsync(reviewer, submitted);
        using var anonymous = fixture.AnonymousClient();

        var before = await SearchPublicAsync(anonymous, token);
        SpecAssert.True(before.Any(value => value.Id == published.Id), "Published Question must be visible before withdrawal.");

        var withdrawn = await WithdrawAsync(reviewer, published.Id, "publication withdrawn for lifecycle readiness");
        SpecAssert.Equal(QuestionStatus.Withdrawn, withdrawn.Status, "Question must enter WITHDRAWN lifecycle state.");

        using (var detail = await anonymous.GetAsync($"/api/public/questions/{published.Id}"))
        {
            SpecAssert.Equal(HttpStatusCode.NotFound, detail.StatusCode, "WITHDRAWN Question body must no longer resolve through the public boundary.");
        }
        var after = await SearchPublicAsync(anonymous, token);
        SpecAssert.False(after.Any(value => value.Id == published.Id), "WITHDRAWN Question must disappear from public search.");

        using var administrative = await reviewer.GetAsync($"/api/admin/questions/{published.Id}");
        SpecAssert.Equal(HttpStatusCode.OK, administrative.StatusCode, "The lifecycle record must remain administratively available after public withdrawal.");
        var administrativeQuestion = await ReadQuestionAsync(administrative);
        SpecAssert.Equal(QuestionStatus.Withdrawn, administrativeQuestion.Status, "Administrative lifecycle state must remain WITHDRAWN for the same stable ID.");
    })
};

return await SpecTestRunner.RunAsync("ToiNoMori town-readiness specification tests", tests);

static string UniqueToken(string prefix) => $"{prefix}-{Guid.NewGuid().ToString("N")[..12]}";

static QuestionContentRequest ValidContent(string suffix) =>
    new($"question {suffix}", $"body {suffix}", ["town-readiness", suffix]);

static async Task<QuestionResponse> CreateDraftAsync(HttpClient client, string suffix)
{
    using var response = await client.PostAsJsonAsync("/api/admin/questions", ValidContent(suffix));
    SpecAssert.Equal(HttpStatusCode.Created, response.StatusCode, "Town-readiness precondition create must succeed.");
    return await ReadQuestionAsync(response);
}

static async Task<QuestionResponse> SubmitAsync(HttpClient client, Guid id)
{
    using var response = await client.PostAsync($"/api/admin/questions/{id}/submit", null);
    SpecAssert.Equal(HttpStatusCode.OK, response.StatusCode, "Town-readiness precondition submit must succeed.");
    return await ReadQuestionAsync(response);
}

static async Task<QuestionResponse> ApproveAsync(HttpClient client, QuestionResponse submitted)
{
    using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/admin/questions/{submitted.Id}/approve")
    {
        Content = new StringContent(string.Empty, Encoding.UTF8, "application/json")
    };
    request.Headers.Add("Idempotency-Key", $"tr-approve-{submitted.Id:N}-{submitted.Version}");
    request.Headers.TryAddWithoutValidation("If-Match", $"\"{submitted.Version}\"");
    using var response = await client.SendAsync(request);
    SpecAssert.Equal(HttpStatusCode.OK, response.StatusCode, "Town-readiness precondition approve must succeed.");
    return await ReadQuestionAsync(response);
}

static async Task<QuestionResponse> WithdrawAsync(HttpClient client, Guid id, string reason)
{
    using var response = await client.PostAsJsonAsync(
        $"/api/admin/questions/{id}/withdraw",
        new ReviewReasonRequest(reason));
    SpecAssert.Equal(HttpStatusCode.OK, response.StatusCode, "Town-readiness precondition withdraw must succeed.");
    return await ReadQuestionAsync(response);
}

static async Task<QuestionResponse> ReadQuestionAsync(HttpResponseMessage response)
{
    return await response.Content.ReadFromJsonAsync<QuestionResponse>(QuestionJsonOptions())
        ?? throw new TestFailureException("Question response JSON was empty.");
}

static async Task<PublicQuestionResponse[]> SearchPublicAsync(HttpClient client, string query)
{
    using var response = await client.GetAsync($"/api/public/questions?query={Uri.EscapeDataString(query)}&limit=50");
    SpecAssert.Equal(HttpStatusCode.OK, response.StatusCode, "Public search must remain available.");
    return await response.Content.ReadFromJsonAsync<PublicQuestionResponse[]>()
        ?? throw new TestFailureException("Public Question list JSON was empty.");
}

static JsonSerializerOptions QuestionJsonOptions()
{
    var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
    options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseUpper));
    return options;
}

file static class PublicQuestionFieldAllowlist
{
    public static readonly string[] Names = ["body", "id", "publishedAt", "tags", "title"];
}

file sealed record QuestionResponse(
    Guid Id,
    string Title,
    string Body,
    IReadOnlyList<string> Tags,
    QuestionStatus Status,
    int Version,
    string? OwnerSubject,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? PublishedAt,
    string? ReviewReason,
    string? WithdrawalReason);
