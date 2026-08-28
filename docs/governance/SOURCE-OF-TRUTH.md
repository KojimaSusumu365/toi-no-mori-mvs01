# Source of truth and identity ledger

## 正本の優先順位

1. Git objectで固定された実装・test
2. そのobjectを実行したGitHub ActionsのRun / attempt / Job / artifact
3. Stage manifestとevidence文書
4. review文書
5. 会話要約

会話に書かれたSHAや件数がGitHubと衝突する場合、GitHubの型付き識別子を採用し、不一致をFindingとして残します。

## Stage 6R-11R review target

| 型 | 値 | 用途 |
|---|---|---|
| implementation commit | `61b55e03d1c3df7355eb3cf15aa1f1fcad7870e1` | Claudeがreviewする固定実装HEAD |
| implementation tree | `23de94ef1e6ded9e2122b11880b7cb80ff8378ae` | 上記commitが参照するtree object |
| base commit | `60c10feb1fed4b4b5000fac4145aa4def591877f` | stacked PR #4のbase |
| evaluated PR merge ref | `83857ee48d4f5317dddf0023a8821a67e3e62980` | workflowがcheckoutしたbase+headの仮想merge |
| relationship | `pull_request_merge_ref` | headがmerge refのancestorであることをworkflowが検証 |
| workflow run | `33135504039` attempt 1 | Stage 6R-11実行 |
| job | `98734412669` | 90/90 gate job |
| artifact | `9671907000` | immutable evidence artifact |
| artifact digest | `sha256:fd4bd48943a0d9c6fb4f3fb20622856503f2f2783070da2070f3cd85878a1955` | artifact integrity |

`23de94ef1e6ded9e2122b11880b7cb80ff8378ae` はtree objectです。branch HEAD commitと記載してはいけません。  
`83857ee48d4f5317dddf0023a8821a67e3e62980` はPR merge refです。implementation HEADと記載してはいけません。

Review packetをsealする後続documentation commitは、target実装を変更しません。Claudeのreview対象は常に `61b55e03d1c3df7355eb3cf15aa1f1fcad7870e1` です。

## Taxonomy overlay evidence

| Type | Value |
|---|---|
| overlay commit | `80090e2eb56c4ddf438867572f8f6e8c389813ba` |
| overlay tree | `5829359435e7d07a17196182653cbc72ae93e641` |
| base / parent | `b85459ff1db304346e159e75833b1c415ce7a575` |
| Stage 6R-10 Run / Job | `33139913725 / 98748216295` — success, historical 85/85 writer |
| Stage 6R-10 artifact | `9673573611`, `sha256:02922ab03a11eec9dab41141fff03dc7b996f53542b9762a3a0d7330f61ee155` |
| navigation/taxonomy Run / Job | `33139913729 / 98748216209` — success |
| Stage 6R-11 Run / Job | `33139913757 / 98748216596` — success, 90/90 |
| Stage 6R-11 artifact | `9673576028`, `sha256:16c7a9e3f6b52674eaec601a5ac70f41a173c7af59ab01743ff85f6dcccc3ea8` |

The GitHub API was rechecked on 2026-08-28. Each artifact identifies
`stage-gh-org-1-physical-taxonomy@80090e2` as its head. The navigation job has no
artifact by design; its Run, Job, head commit and successful steps are the identity.

## Baseline chain

- `main@c90dfdb154d99ee480571c8a397e99d88e12dea8` remains unchanged.
- PR #1 is the cumulative Stage 6R-1〜11 baseline.
- PR #3 adds repository navigation on top of PR #1.
- PR #4 adds Stage 6R-11R on top of PR #3.
- PR #5 adds the physical taxonomy on top of PR #4.
- No PR is merged and all remain Draft.

## Deferred-test source of truth

`spec/deferred-tests.json` is the machine-readable source of truth for every
current not-run test, including owner, reason and due condition. Review manifests
and evidence tables must copy those values exactly and may not define alternatives.

## 必須台帳項目

各受入evidenceは次を同じレコードに保存します。

- stage
- repository
- branch
- head commit
- base commit
- PR merge ref commit（PR event時）
- workflow name / workflow id
- run id / attempt
- job id / gate name
- expected / passed / failed / not-run
- artifact name / digest / retention
- headと実評価対象のrelationship
- captured_at

## relationship値

- `same_commit`: checkout対象が記録commitと同じ
- `pull_request_merge_ref`: baseとheadの仮想mergeを評価
- `different_verified_commit`: 別commitだが関係を証明済み
- `unknown`: 証明できない。受入根拠にしない

PR eventではAPIの `head_sha` だけからcheckout対象を断定しません。merge ref、workflow checkout設定、ancestor検査を同じ証跡に残します。
