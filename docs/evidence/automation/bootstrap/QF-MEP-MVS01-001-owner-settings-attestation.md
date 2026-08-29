# Repository Owner signed-in settings attestation

- 文書ID: `QF-MEP-MVS01-001-OSA1`
- 関連資料: `QF-MEP-MVS01-001-MR1`、`QF-MEP-MVS01-001-BMA1`
- repository: `KojimaSusumu365/toi-no-mori-mvs01`
- collection window (UTC): `2026-08-29T16:10:35.539Z` ～ `2026-08-29T16:10:39.953Z`
- collection window (JST): `2026-08-30T01:10:35.539+09:00` ～ `2026-08-30T01:10:39.953+09:00`
- session evidence: repository Settingsページへのsigned-inアクセスを確認。画面見出しは`Settings: KojimaSusumu365/toi-no-mori-mvs01`
- execution boundary: **read-only**。GitHub上の設定、secret、variable、App、PR、branch、reviewは変更していない
- data minimization: secret値はGitHub画面にも表示されず取得していない。variable値はDOMから抽出せず、名前と空状態だけを取得した

## 結論

`QF-MEP-MVS01-001-BMA1`で`NOT_MEASURED`だったRepository Owner設定項目をsigned-in sessionで実測した。
Repository／EnvironmentのActions secretsとvariablesは空で、`QF_AI_PHASE`および対象4 secret名は不存在だった。
可視のInstalled GitHub Apps一覧にもQF publisher用Appは存在しなかった。

Actionsは既定`GITHUB_TOKEN`がrepository contents/packagesのread-onlyであり、GitHub Actionsによる
pull requestの作成・承認は無効だった。外部contributor workflowはfirst-time contributorに承認を要求する。
一方、repository policyは全actions/reusable workflowsを許可し、full-length commit SHA固定を強制していない。
これは設定変更を行わず、観測リスクとして記録する。

この資料はowner-settings measurementの完了証拠であり、merge、Draft解除、retarget、Phase A開始、
Stage 6R-12開始を承認しない。`QF-MEP-MVS01-001-BMA1`の全体判定は引き続き
**`NO_GO_NOW / CONDITIONAL_BOOTSTRAP_CANDIDATE`**である。

## Actions settings

観測時刻: `2026-08-29T16:10:36.458Z` (`2026-08-30T01:10:36.458+09:00`)

| 項目 | signed-in live観測値 | 評価 |
|---|---|---|
| Actions permissions | `Allow all actions and reusable workflows` | OBSERVED / broad allowance |
| Require full-length commit SHA | `false` | OBSERVED / repository-level enforcementなし |
| Artifact and log retention | `90 days` | OBSERVED |
| Fork PR workflow approval | `Require approval for first-time contributors` | OBSERVED |
| Default workflow token | `Read repository contents and packages permissions` | PASS / read-only default |
| Actions create/approve PR | `false` | PASS / disabled |

`Allow actions created by GitHub`および`Allow actions by Marketplace verified creators`のcheckboxは、
全actions許可radioが選択された状態でuncheckedだった。これらは限定許可モード用の従属controlであり、
全actions許可という実効観測を上書きしない。

## Actions secrets

観測時刻: `2026-08-29T16:10:37.313Z` (`2026-08-30T01:10:37.313+09:00`)

- Environment secrets: `This environment has no secrets.`
- Repository secrets: `This repository has no secrets.`
- secret値: **未表示・未取得**

| BMA1対象secret名 | 存在 |
|---|---|
| `OPENAI_API_KEY` | `false` |
| `ANTHROPIC_API_KEY` | `false` |
| `QF_GITHUB_APP_ID` | `false` |
| `QF_GITHUB_APP_PRIVATE_KEY` | `false` |

## Actions variables

観測時刻: `2026-08-29T16:10:38.113Z` (`2026-08-30T01:10:38.113+09:00`)

- Environment variables: `This environment has no variables.`
- Repository variables: `This repository has no variables.`
- variable値: **抽出・記録していない**
- Repository variable `QF_AI_PHASE`: `present=false`
- したがって、live repository variableによる`Phase A`指定は存在しない

## Installed GitHub Apps

観測時刻: `2026-08-29T16:10:38.939Z` (`2026-08-30T01:10:38.939+09:00`)

repository Settingsの`Installed GitHub Apps`ページで次の可視一覧を確認した。

- `ChatGPT Codex Connector`
- `Claude`
- `Cursor`
- `Devin.ai Integration`
- `Slack`

QF publisherを示すApp名は可視一覧に存在しない。BMA1はQF Appのcanonical App名またはApp IDを
指定していないため、本証拠は「可視一覧にQF publisher用Appがない」ことを記録し、無関係な既存Appの
repository selectionやpermissionまでは推定しない。対象QF Appがないため、そのConfigure画面は存在せず、
どの既存Appについても`Configure`を開いていない。

既存のBMA1 live証拠ではrulesetsが`[]`、default branch protectionが`false`であるため、
QF Appのruleset／branch-protection bypass actorも観測されていない。

## BMA1 gap closure

| BMA1項目 | 旧状態 | OSA1実測 |
|---|---|---|
| `QF_AI_PHASE` repository variable | `NOT_MEASURED` | repository variables空、`present=false` |
| Actions default token permission | `NOT_MEASURED` | contents/packages read-only |
| Actions create/approve PR | `NOT_MEASURED` | disabled |
| fork PR Actions approval | `NOT_MEASURED` | first-time contributorsに承認要求 |
| QF secret names | `NOT_MEASURED` | repository/environment secrets空、4件すべて不存在 |
| QF GitHub App installation | `NOT_MEASURED` | 可視Installed Apps一覧にQF publisher Appなし |

Owner settings checkpointは`MEASURED_COMPLETE`とする。ただし次は変わらない。

- PR #1・#3〜#7: Draft / OPEN / unmerged
- PR retarget: 未実行
- `main`: 未更新
- Controller: `BOOTSTRAP_DISABLED`
- Reviewer: `VACANT / PENDING ACTIVATION`
- P3-015: `OPEN / DEFERRED`
- secrets / variables / GitHub Apps / rules / branch protection: 変更なし
- Phase A: 禁止・未開始
- Stage 6R-12: `NOT STARTED`

型付き証拠は`QF-MEP-MVS01-001-owner-settings-evidence.json`へ保存した。
