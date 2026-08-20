using ToiNoMori.Domain;
using ToiNoMori.Testing;

internal static class Stage6R2DomainTests
{
    private static readonly Guid TenantId = Guid.Parse("7b48e239-07ef-4b34-a1fb-7f4fc7ff1673");

    public static IReadOnlyList<SpecTest> Create() =>
    [
        new("TC-ACC-MVS01-063-DOM", "ADR-0008-D1,D2", "承認対象版を固定し古い版の承認を拒否", ApprovalMustBindToReviewedVersion),
        new("TC-ACC-MVS01-079-DOM", "DOMAIN-INVARIANTS", "決定論的ランダム操作で集約不変条件を維持", AggregateInvariantsMustHold),
        new("TC-ACC-MVS01-081-DOM", "ADR-0008-D4", "差戻し理由と取下げ理由を分離", ReviewAndWithdrawalReasonsMustBeSeparate)
    ];

    private static Task ApprovalMustBindToReviewedVersion()
    {
        var question = NewQuestion(0);
        question.Submit("editor-a", At(1));
        var firstReviewedVersion = question.Version;
        question.ReturnForChanges("reviewer-b", "根拠を追記してください", At(2));
        question.Update("改訂版", "根拠を追記した本文", ["cloud", "security"], question.Version, "editor-a", At(3));
        question.Submit("editor-a", At(4));

        var beforeBlankReviewer = question.Snapshot();
        var reviewerRequired = SpecAssert.Throws<DomainRuleViolationException>(
            () => question.Approve(" ", question.Version, At(5)),
            "A blank reviewer must fail.");
        SpecAssert.Equal("question.reviewer.required", reviewerRequired.Code, "The reviewer-required code must be stable.");
        AssertSnapshotEqual(beforeBlankReviewer, question.Snapshot(), "A rejected blank reviewer must be atomic.");

        var beforeStaleApproval = question.Snapshot();
        var conflict = SpecAssert.Throws<DomainRuleViolationException>(
            () => question.Approve("reviewer-b", firstReviewedVersion, At(6)),
            "The approval of a stale reviewed version must fail.");
        SpecAssert.Equal("question.version.conflict", conflict.Code, "The conflict code must be stable.");
        AssertSnapshotEqual(beforeStaleApproval, question.Snapshot(), "A rejected approval must be atomic.");

        var approvedVersion = question.Version;
        question.Approve("reviewer-b", approvedVersion, At(7));
        SpecAssert.Equal(QuestionStatus.Published, question.Status, "A current approval must publish the question.");
        SpecAssert.Equal<int?>(approvedVersion, question.ApprovedVersion, "Approval must retain the reviewed version.");
        SpecAssert.Equal("reviewer-b", question.ApprovedBy, "Approval must retain the reviewer.");
        SpecAssert.Equal(approvedVersion + 1, question.Version, "Approval must advance the aggregate version once.");
        SpecAssert.Equal(TenantId, question.TenantId, "Approval must not change the tenant.");
        return Task.CompletedTask;
    }

    private static Task AggregateInvariantsMustHold()
    {
        SpecAssert.Throws<ArgumentException>(
            () => _ = new Question(Guid.NewGuid(), Guid.Empty, "title", "body", [], "editor-a", At(0)),
            "An empty tenant identifier must be rejected at construction.");

        var random = new Random(20260820);
        for (var sequence = 0; sequence < 500; sequence++)
        {
            var question = NewQuestion(sequence);
            for (var operation = 0; operation < 20; operation++)
            {
                var before = question.Snapshot();
                try
                {
                    ExecuteRandomOperation(question, random, sequence, operation);
                    var after = question.Snapshot();
                    SpecAssert.Equal(before.Version + 1, after.Version, "A successful command must advance the version exactly once.");
                    SpecAssert.Equal(before.TenantId, after.TenantId, "A command must not change tenant identity.");
                    AssertPublishedMetadata(after);
                }
                catch (DomainRuleViolationException)
                {
                    AssertSnapshotEqual(before, question.Snapshot(), "A rejected command must not partially mutate the aggregate.");
                }
            }
        }

        return Task.CompletedTask;
    }

    private static Task ReviewAndWithdrawalReasonsMustBeSeparate()
    {
        var question = NewQuestion(1);
        question.Submit("editor-a", At(1));
        question.ReturnForChanges("reviewer-b", "設計根拠を追記", At(2));
        SpecAssert.Equal("設計根拠を追記", question.ReviewReason, "Return must retain only the review reason.");
        SpecAssert.Equal<string?>(null, question.WithdrawalReason, "Return must not set a withdrawal reason.");

        question.Update("改訂版", "設計根拠を追記した本文", ["cloud"], question.Version, "editor-a", At(3));
        question.Submit("editor-a", At(4));
        question.Approve("reviewer-b", question.Version, At(5));

        var beforeBlankReason = question.Snapshot();
        var reasonRequired = SpecAssert.Throws<DomainRuleViolationException>(
            () => question.Withdraw("reviewer-b", " ", At(6)),
            "A blank withdrawal reason must fail.");
        SpecAssert.Equal("question.withdrawal.reason_required", reasonRequired.Code, "The withdrawal-reason code must be stable.");
        AssertSnapshotEqual(beforeBlankReason, question.Snapshot(), "A rejected withdrawal must be atomic.");

        question.Withdraw("reviewer-b", "掲載期限終了", At(6));

        SpecAssert.Equal(QuestionStatus.Withdrawn, question.Status, "Withdraw must enter WITHDRAWN.");
        SpecAssert.Equal<string?>(null, question.ReviewReason, "Withdrawal must not reuse the review reason.");
        SpecAssert.Equal("掲載期限終了", question.WithdrawalReason, "Withdrawal must retain its own reason.");
        return Task.CompletedTask;
    }

    private static void ExecuteRandomOperation(Question question, Random random, int sequence, int operation)
    {
        var now = At(10 + (sequence * 20) + operation);
        switch (random.Next(5))
        {
            case 0:
                var expected = random.Next(2) == 0 ? question.Version : StaleVersion(question.Version);
                var actor = random.Next(3) == 0 ? "other-editor" : "editor-a";
                question.Update($"title-{sequence}-{operation}", $"body-{sequence}-{operation}", ["cloud"], expected, actor, now);
                break;
            case 1:
                question.Submit(random.Next(3) == 0 ? "other-editor" : "editor-a", now);
                break;
            case 2:
                question.ReturnForChanges("reviewer-b", $"return-{sequence}-{operation}", now);
                break;
            case 3:
                var reviewedVersion = random.Next(2) == 0 ? question.Version : StaleVersion(question.Version);
                var reviewer = random.Next(4) == 0 ? "editor-a" : "reviewer-b";
                question.Approve(reviewer, reviewedVersion, now);
                break;
            default:
                question.Withdraw("reviewer-b", $"withdraw-{sequence}-{operation}", now);
                break;
        }
    }

    private static int StaleVersion(int currentVersion) => currentVersion == 1 ? 2 : currentVersion - 1;

    private static void AssertPublishedMetadata(QuestionSnapshot snapshot)
    {
        if (snapshot.Status is not (QuestionStatus.Published or QuestionStatus.Withdrawn))
        {
            return;
        }

        SpecAssert.NotNull(snapshot.ApprovedVersion, "Published history must identify the approved version.");
        SpecAssert.True(snapshot.ApprovedVersion <= snapshot.Version, "Approved version must not exceed aggregate version.");
        SpecAssert.True(!string.IsNullOrWhiteSpace(snapshot.ApprovedBy), "Published history must identify the reviewer.");
    }

    private static void AssertSnapshotEqual(QuestionSnapshot expected, QuestionSnapshot actual, string message)
    {
        var equal = expected.Id == actual.Id
            && expected.TenantId == actual.TenantId
            && expected.Title == actual.Title
            && expected.Body == actual.Body
            && expected.Tags.SequenceEqual(actual.Tags, StringComparer.Ordinal)
            && expected.Status == actual.Status
            && expected.Version == actual.Version
            && expected.OwnerSubject == actual.OwnerSubject
            && expected.CreatedAt == actual.CreatedAt
            && expected.UpdatedAt == actual.UpdatedAt
            && expected.PublishedAt == actual.PublishedAt
            && expected.ReviewReason == actual.ReviewReason
            && expected.WithdrawalReason == actual.WithdrawalReason
            && expected.ApprovedVersion == actual.ApprovedVersion
            && expected.ApprovedBy == actual.ApprovedBy;
        SpecAssert.True(equal, message);
    }

    private static Question NewQuestion(int sequence) => new(
        Guid.Parse($"00000000-0000-0000-0000-{sequence + 1:D12}"),
        TenantId,
        "first question",
        "question body",
        ["cloud"],
        "editor-a",
        At(0));

    private static DateTimeOffset At(int minute) => new DateTimeOffset(2026, 8, 20, 0, 0, 0, TimeSpan.Zero).AddMinutes(minute);
}
