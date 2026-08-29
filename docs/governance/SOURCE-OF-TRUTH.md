# Source of truth and identity ledger

## 正本の優先順位

1. Git objectで固定された実装・test
2. そのobjectを実行したGitHub ActionsのRun / attempt / Job / artifact
3. Stage manifestとevidence文書
4. review文書
5. 会話要約

会話に書かれたSHAや件数がGitHubと衝突する場合、GitHubの型付き識別子を採用し、不一致をFindingとして残します。

## 識別子を混同しない

| 型 | 現在値 | 用途 |
|---|---|---|
| commit | `4537085c25ed3178214b0693afac7e42ce1b64de` | 現在のbranch HEAD |
| tree | `4402dd93d1a50fe58e96d0fa0242e30cdcc6450e` | commitが参照するtree |
| parent commit | `07815c1a9b22c437c72a991fe120a1f8be61bc9e` | functional codeを含む親 |
| PR merge ref commit | `3a3ff47d7972ad5fee7e9c5062e2267539c52429` | PR #1のbaseとの仮想merge |
| workflow run | `33002851599` | Stage 6R-11実行 |
| run attempt | `1` | retry単位 |
| job | `98288871317` | 90/90 gate job |
| test id | 例: `TC-ACC-MVS01-067-PG` | 個別受入test |

`4402dd93d1a50fe58e96d0fa0242e30cdcc6450e` はtree objectです。「branch HEAD commit」と記載してはいけません。

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

PR eventではAPIの `head_sha` だけからcheckout対象を断定しません。merge refとworkflow checkout設定も記録します。
