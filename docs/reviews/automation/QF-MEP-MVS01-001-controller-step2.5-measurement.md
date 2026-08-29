# Controller Step 2.5 live measurement実施パケット

- 文書ID: QF-MEP-MVS01-001
- 種別: Measurement Execution Packet
- 対象PR: #7 `ops-github-autodrive-controller`
- 固定implementation SHA: `dcfc9e03cd82da07d9da3ad841fb13f9c9ed850d`
- 固定implementation tree: `ab04ccd8f4415ad4188917264cc20309dfbd04a9`
- 技術レビュー: QF-RVR-MVS01-020 — `PASS_WITH_FINDINGS / blocking=false`
- Organizer処置: QF-ORG-MVS01-004 — `CLOSED_VERIFIED 21 / OPEN_DEFERRED 1`
- 状態: `PLANNED / NOT AUTHORIZED TO ENABLE`
- 作成日: 2026-08-29

## 目的

静的検証では確定できないGitHubの実設定とevent coverageをlive環境で測定し、
`AUTO-IMPL-P3-015`と`TC-ACC-MVS01-092-STEP`の終了判断に必要な型付き証拠を作る。
本書は計測計画であり、PR merge、Draft解除、Phase A、secrets/App設定、Stage開始を
許可しない。

## 現在のEnablement gate

| Gate | 状態 | 次の証拠 |
|---:|---|---|
| 1. 先行PR chainのmerge | 未充足 | PR #1、#3〜#6の順序付きmerge記録 |
| 2. Independent Automation Release Reviewer任命 | 未充足（`VACANT`） | 固定SHAへの別人の独立署名、初回はTC-094 |
| 3. Step 2.5 / `NOT_MEASURED`解消 | 未充足 | 本書の測定結果 |
| 4. 固定SHAで40 test | 充足 | `dcfc9e03…`で40/40 |
| 5. 同一SHAのClaude技術レビュー | 充足 | QF-RVR-MVS01-020 |
| 6. 独立人間署名 + Organizer final acceptance | 未充足 | 署名記録と後続Organizer acceptance |
| 7. governance PRの手動merge | 未充足 | Gate 1〜6後の別承認 |
| 8. secrets/App/rules設定 | 未充足 | merge後の別途明示承認 |

## 実行前提

- Repository Ownerが計測窓と対象repositoryを明示する。
- Independent Automation Release ReviewerはOrganizer/Codex/Claudeとは別の人間とする。
- Reviewerへwrite権限を付与せず、Organizer allowlistへ追加しない。
- PR #7はDraft、`QF_AI_PHASE`はunset、Controller baselineは
  `BOOTSTRAP_DISABLED`のままとする。
- secrets、App、ruleset、branch protectionを変更する試験は、その変更を明示承認した
  別操作として実施する。観測だけで足りる項目はread-onlyで取得する。
- 実行前にPR head、base、fixed implementation commit/treeを再取得し、drift時は停止する。

## Track A — bootstrap独立署名

`TC-ACC-MVS01-094-BOOTSTRAP`として、Independent Automation Release Reviewerが
固定implementation SHA `dcfc9e03…`、tree `ab04ccd8…`、QF-RVR-MVS01-020、
本Organizer処置を読み、署名時刻、GitHub login、対象SHA、結論、残存リスクを記録する。
署名は任命またはPhase A有効化そのものではない。署名者がOrganizer allowlist外かつ
write権限なしであることも同じ証拠へ保存する。

## Track B — P3-015 event coverage

次のactivityごとに、current head SHAとCheck Suite / Check Runの対応をlive計測する。

| Event | Activity | 期待 |
|---|---|---|
| `pull_request` | `opened` | role appointment Required Checkが評価される |
| `pull_request` | `reopened` | 同Checkが再評価される |
| `pull_request` | `synchronize` | 新しいcurrent head SHAへ再評価される |
| `pull_request` | `ready_for_review` | Draft解除イベントで再評価される。ただしPR #7自体のDraft解除には使わない |
| `pull_request_review` | `submitted` | current headへのAPPROVEDを評価する |
| `pull_request_review` | `edited` | 変更後のreview stateを再評価する |
| `pull_request_review` | `dismissed` | 取消後にREDへ転じる |

appointment対象変更と非対象変更の両方で、path filterによるskipがなく、
`applicable / not_applicable / indeterminate`の結果と根拠が返ることを確認する。
appointment対象では被任命者のcurrent-head `APPROVED`だけがGREENとなり、旧SHA承認、
CHANGES_REQUESTED、DISMISSED、不完全pagination、API失敗はfail-closed REDとする。

## Track C — Step 2.5共通測定

凍結仕様§17の各項目について、推測ではなくAPI、設定画面、Check RunまたはAction logの
実測値を記録する。

- repository visibility、fork PR Actions承認設定
- default `GITHUB_TOKEN` permission、ActionsによるPR承認可否
- branch protection Required Checksとdefault branch workflow集合
- GitHub Actions費用条件、artifact/log retention実測日数
- Job Bの`contents: read` checkout可否
- Claude Action認証profileと`id-token: write`要否
- GitHub Appのruleset bypassとWorkflows権限`none`
- Work Order push triggerが参照するworkflow SHA
- `pull-requests: write`だけでのlabel付与可否、`issues: write`が`none`
- Review Resultの永続path、content hash、append-only、公開範囲、publisher権限
- Gate / Precondition / Stop Registryのhashと実装対応
- Disposition Recordのappend-only、actor allowlist、content hash検査

label smoke testが失敗しても`issues: write`を自動追加しない。必要権限が異なる場合は
結果をREDとして記録し、別のgovernance判断へ戻す。

## 証拠レコード必須項目

各観測を同じappend-only recordへ保存する。

- repository、branch、PR number、base/head SHA、fixed implementation SHA/tree
- event名、activity、delivery/run/check-suite/check-run ID、attempt、時刻
- workflow名とworkflow SHA、job名、conclusion、required-check名
- changed file set、appointment applicability、reviewer login/state/commit SHA
- reviewer permission、Organizer allowlist membership、pagination completeness
- API success/failure、観測値、期待値、PASS/FAIL/NOT_MEASURED
- evidence source URL、artifact ID/digest、取得actor、captured_at
- P3-015をCLOSE可能と判断した根拠、または残ったgap

## 繰延台帳との接続

| Test ID | owner | due | 本工程での扱い |
|---|---|---|---|
| TC-ACC-MVS01-091-REVERIFY | Organizer | Before Phase B enablement | Phase B前まで継続 |
| TC-ACC-MVS01-092-STEP | Independent Automation Release Reviewer | Step 2.5, before Phase A enablement | 本書の主対象 |
| TC-ACC-MVS01-093-DISPOSITION | Organizer | Before Phase A enablement | publication transportを別測定 |
| TC-ACC-MVS01-094-BOOTSTRAP | Organizer | Before initial governance PR merge | Track Aで人手署名 |

## 完了条件と次のOrganizer判断

P3-015は、Track Bの全activityがliveで観測され、Required Checkがcurrent headへ結び付き、
承認の有効化と取消のRED遷移が再現され、証拠recordがappend-onlyで保存された場合にのみ
Organizer CLOSE候補となる。不足が1つでもあれば`OPEN / DEFERRED`を維持する。

計測完了後は、Independent Automation Release Reviewerの署名、QF-020、QF-ORG-004、
測定結果、残存`NOT_MEASURED`、PR/branch-protection状態をまとめた別のOrganizer final
acceptanceを作る。それまではPR #7のmerge、Draft解除、Phase A、Stage 6R-12を開始しない。

