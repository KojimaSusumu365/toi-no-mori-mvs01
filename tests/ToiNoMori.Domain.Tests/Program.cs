using ToiNoMori.Domain;
using ToiNoMori.Testing;

var tests = new List<SpecTest>
{
    new("TC-ACC-MVS01-003", "REQ-MVS01-QST-001", "作成時は DRAFT・version=1", () =>
    {
        var question = NewQuestion();
        SpecAssert.Equal(QuestionStatus.Draft, question.Status, "Initial status must be DRAFT.");
        SpecAssert.Equal(1, question.Version, "Initial version must be one.");
        return Task.CompletedTask;
    }),
    new("TC-ACC-MVS01-006", "REQ-MVS01-QST-002", "所有者の更新で version を進める", () =>
    {
        var question = NewQuestion();
        question.Update("updated", "updated body", ["design"], 1, "editor-a", Now().AddMinutes(1));
        SpecAssert.Equal(2, question.Version, "Update must increment the version.");
        SpecAssert.Equal("updated", question.Title, "Update must replace the title.");
        return Task.CompletedTask;
    }),
    new("TC-ACC-MVS01-007", "REQ-MVS01-QST-002", "古い版の更新を競合として拒否", () =>
    {
        var question = NewQuestion();
        question.Update("v2", "body", [], 1, "editor-a", Now().AddMinutes(1));
        var exception = SpecAssert.Throws<DomainRuleViolationException>(
            () => question.Update("stale", "body", [], 1, "editor-a", Now().AddMinutes(2)),
            "A stale update must fail.");
        SpecAssert.Equal("question.version.conflict", exception.Code, "The conflict code must be stable.");
        return Task.CompletedTask;
    }),
    new("TC-ACC-MVS01-008", "REQ-MVS01-WF-001", "DRAFT から IN_REVIEW へ申請", () =>
    {
        var question = NewQuestion();
        question.Submit("editor-a", Now().AddMinutes(1));
        SpecAssert.Equal(QuestionStatus.InReview, question.Status, "Submit must enter IN_REVIEW.");
        return Task.CompletedTask;
    }),
    new("TC-ACC-MVS01-009", "REQ-MVS01-WF-001", "未定義の状態遷移を拒否", () =>
    {
        var question = NewQuestion();
        question.Submit("editor-a", Now().AddMinutes(1));
        var exception = SpecAssert.Throws<DomainRuleViolationException>(
            () => question.Submit("editor-a", Now().AddMinutes(2)),
            "Submitting twice must fail.");
        SpecAssert.Equal("question.submit.invalid_state", exception.Code, "The transition code must be stable.");
        return Task.CompletedTask;
    }),
    new("TC-ACC-MVS01-010", "REQ-MVS01-WF-002", "作成者本人の自己承認を拒否", () =>
    {
        var question = NewQuestion();
        question.Submit("editor-a", Now().AddMinutes(1));
        var exception = SpecAssert.Throws<DomainRuleViolationException>(
            () => question.Approve("editor-a", Now().AddMinutes(2)),
            "Self approval must fail.");
        SpecAssert.Equal("question.approve.self_forbidden", exception.Code, "The self-approval code must be stable.");
        return Task.CompletedTask;
    }),
    new("TC-ACC-MVS01-012", "REQ-MVS01-WF-002", "理由付き差戻しで DRAFT へ戻す", () =>
    {
        var question = NewQuestion();
        question.Submit("editor-a", Now().AddMinutes(1));
        question.ReturnForChanges("reviewer-b", "根拠を追記してください", Now().AddMinutes(2));
        SpecAssert.Equal(QuestionStatus.Draft, question.Status, "Return must enter DRAFT.");
        SpecAssert.Equal("根拠を追記してください", question.ReviewReason, "Return reason must be retained.");
        return Task.CompletedTask;
    }),
    new("TC-ACC-MVS01-013", "REQ-MVS01-WF-002", "別担当者の承認で公開日時を確定", () =>
    {
        var question = NewQuestion();
        question.Submit("editor-a", Now().AddMinutes(1));
        question.Approve("reviewer-b", Now().AddMinutes(2));
        SpecAssert.Equal(QuestionStatus.Published, question.Status, "Approval must publish the question.");
        SpecAssert.NotNull(question.PublishedAt, "Approval must set PublishedAt.");
        return Task.CompletedTask;
    }),
    new("TC-ACC-MVS01-016", "REQ-MVS01-WD-001", "PUBLISHED から WITHDRAWN へ取り下げ", () =>
    {
        var question = NewQuestion();
        question.Submit("editor-a", Now().AddMinutes(1));
        question.Approve("reviewer-b", Now().AddMinutes(2));
        question.Withdraw("reviewer-b", "公開終了", Now().AddMinutes(3));
        SpecAssert.Equal(QuestionStatus.Withdrawn, question.Status, "Withdraw must enter WITHDRAWN.");
        return Task.CompletedTask;
    })
};

tests.AddRange(Stage6R2DomainTests.Create());

return await SpecTestRunner.RunAsync("ToiNoMori.Domain specification tests", tests);

static DateTimeOffset Now() => new(2026, 8, 16, 0, 0, 0, TimeSpan.Zero);

static Question NewQuestion() => new(
    Guid.NewGuid(),
    "first question",
    "question body",
    ["cloud"],
    "editor-a",
    Now());
