# GitHub自動運転Controller 技術レビュー依頼

- 文書ID: QF-RRQ-MVS01-001
- 日付: 2026-08-28
- Organizer: KojimaSusumu365
- Reviewer: Claude（独立技術レビュー）
- Repository: `KojimaSusumu365/toi-no-mori-mvs01`
- Draft PR: #7
- Base branch: `stage6r11r-final-closure`
- Base commit: `b6959e86713c89b37a8d0e8009f402512c02e346`
- Review対象implementation commit: `c5d316063bc16161e2808c02334f331603b20c32`
- Review対象tree: `01a38e0994d04e922a5929258570a5ecaa69450c`
- packet commit: review対象外の後続documentation commit
- 状態: **BOOTSTRAP_DISABLED / NOT ENABLED**

初回implementation `c88b73a059b092cd5e806e58c21f5395678d7046`の
GitHub実Runで、role appointment Checkは期待どおりREDになりましたが、その
Check名がRequired Check Registryに未登録であることをCodexが検出しました。
`c5d3160`は`REQ-009`として登録し、AUTO-T38へ回帰を追加した修正版です。初回
commit/treeはreview対象ではありません。

## 1. Claudeさんへの依頼

QF-OPS-MVS01-001 Version 0.5.1の凍結設計に対し、上記implementation
commit/treeを固定して独立技術レビューしてください。PRの最新headやmerge refを
implementation commitの代わりにしないでください。

読み順:

1. `docs/governance/automation/QF-OPS-MVS01-001-v0.5.1.md`
2. `docs/governance/automation/QF-RVR-MVS01-014-freeze-confirmation.md`
3. `REVIEW.md`
4. `docs/governance/GITHUB-AUTODRIVE-CONTROLLER.md`
5. `.github/ai/registries/`の6 Registry
6. `.github/ai/schemas/`の5 Schema
7. `scripts/ai_controller/core.py`、CLI、39件の受入試験
8. `.github/workflows/qf-*.yml`とrole appointment workflow
9. threat baseline、AI協働規約、Review Protocol

## 2. 実装した境界

- Organizer承認済みWork Orderだけをdefault branch pushから処理
- Work Orderのhash、期限、actor、risk、第二人間承認、budget、重複を検査
- PR/fork/branchをuntrusted inputとして扱い、外部forkはsecret/artifact前にno-op
- Codexを製造、資格情報なしの全試験、検証済みpatch投稿の3 Jobへ分離
- 投稿Jobは検証済みpatchと同一hashだけをDraft PRへ追加し、mergeしない
- 必須CheckをGitHub Checks APIから取得し、成功を仮造しない
- Claudeは固定commit/treeの読取専用技術review。`CLOSED`とOrganizer処置は禁止
- Review Requestのmode/hashとClaude出力を20項目のGate Registryで照合
- Review Resultをcanonical JSONのcontent-addressed append-only Draftへ投稿
- Organizer dispositionをClaude結果とは別のappend-only Recordとして管理
- role appointmentを現在のPR headに対する被任命者本人のAPPROVEDで検証
- Phase Aの自動是正は0回。merge、Draft解除、Stage開始、deployは出力しない

## 3. 製造試験結果

| 検査 | 結果 |
|---|---|
| Controller `AUTO-T01`〜`AUTO-T38` | 39/39 GREEN（T09a/T09bを別caseとして合計39） |
| Registry / Schema parse | GREEN |
| Workflow YAML parse | GREEN |
| Action full-SHA pin検査 | GREEN |
| Repository navigation | GREEN、required file 55件 |
| Repository taxonomy / local link | GREEN、required file 47件 |
| Test ID uniqueness | PASSED |
| .NET product build | 0 Warning / 0 Error |
| .NET非DB specification suites | 73/73 GREEN |
| `git diff --check` | GREEN |

ローカルPostgreSQL依存suiteは、checksum固定toolchain取得時に
`ftp.postgresql.org`が2回連続HTTP 502を返したため未実行です。これはGREENと
扱いません。PR #7のStage 6R-10およびStage 6R-11 GitHub Actions 90/90を
製造受入証跡とし、そのRun/Job/artifact identityを別途固定します。

## 4. 明示的な未有効化条件

- Independent Automation Release Reviewerは`VACANT`
- threat baselineのGitHub外部設定は`NOT_MEASURED`を含む
- GitHub Appは未導入、AI secretは未構成
- `QF_AI_PHASE`は未設定
- 既存PR #1/#3/#4/#5/#6はDraftかつ未merge

したがって、現時点で許可されるのは実装、static/local/sandbox試験、Draft PR、
技術レビューまでです。governance PR merge、Phase A、AI資格情報run、Stage
6R-12開始は許可されません。

## 5. 特に反証してほしい点

1. default branch Control PlaneとPR head Data Planeが全経路で分離されているか
2. secretまたはApp tokenを読む前にorigin/hash/identityを十分検証しているか
3. Job B/CまたはClaude Jobがuntrusted repository codeを不必要に実行しないか
4. Required Check集合がRegistry、Work Order、実Check Runで一致するか
5. `workflow_run`を連鎖しても元PRのcommit/tree/PR identityをartifactで保てるか
6. Review Resultのcanonical hash、append-only、再実行dedupが破れないか
7. role appointment reviewの取消、後続commit、write権限、混在変更を拒否できるか
8. 31 Stop、18 Precondition、20 Review GateのRegistryと実装に未接続項目がないか
9. Public repositoryでprompt injectionまたは公開logへの情報露出が残らないか
10. `INITIAL`後に先行Review Resultがdurableになった場合、Phase A実装は安全側の
    `organizer:hold`で停止します。自動`REVERIFY`用FIX_CANDIDATE transportは
    Phase B/Cとして未実装です。この限定がv0.5.1のPhase A受入に適合するか、
    Findingまたは明示的deferが必要か判定してください。

## 6. 回答形式

`technical-review.schema.json@3`の意味に合わせ、各Findingを次で返してください。

```text
ID: AUTO-IMPL-P2-001（severityに合わせてP0〜P3を使用）
Severity: P0 | P1 | P2 | P3
Verification status: OPEN
Disposition: UNDECIDED
Target commit:
Path:
Evidence:
Risk:
Required change:
Residual risk:
```

全体判定は`PASS | PASS_WITH_FINDINGS | FAIL`、coverage、unverifiedを明記して
ください。Claudeさんは`CLOSED`、Organizer disposition、merge、Stage PASSを
設定しないでください。
