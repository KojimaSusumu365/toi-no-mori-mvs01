# Stage 6R-10 GitHub Actions受入証跡

- 文書ID: QF-EVD-ST6R10-MVS01-GREEN-001
- 実行日: 2026-08-25
- 判定: **ACCEPTED**
- 対象: 東京–石狩DR切替・復旧証跡のnative local dual-cluster gate

## 1. 受入run

- GitHub Actions Run: #4 / `32802344728`
- Job: `97665554862`
- head SHA: `818a1755065dff0897620e705b9712c341d80110`
- 実行主体: 非root GitHub-hosted runner
- 結論: `success`

| Suite | Passed | Failed |
|---|---:|---:|
| Domain | 12 | 0 |
| API | 41 | 0 |
| Mobile | 7 | 0 |
| OIDC E2E | 8 | 0 |
| PostgreSQL | 12 | 0 |
| DR | 5 | 0 |
| **合計** | **85** | **0** |

Buildはwarning 0 / error 0、テストID一意性はGREEN、Stage 6R-4C〜10のCI構成契約もすべてGREENである。残存failure-first registryは性能契約`TC-PERF-MVS01-002-PG`の1件だけで、期待REDとして分離されている。

## 2. DR受入値

- status: `accepted`
- `isSimulated`: `false`
- `measurementScope`: `native-local-dual-cluster-role-drill`
- `physicalRegionFailover`: `false`
- RPO: 0秒 / 上限3,600秒
- RTO: 2秒 / 上限14,400秒
- DR tests: 5 passed / 0 failed

validatorは`latestSchemaRestored`、`sourceIsolatedBeforeRecovery`、`timelineOrdered`、`twoPersonApproval`、`artifactContainsNoSensitiveKeys`の全項目を`true`とした。migration 005、`fk_published_revision_same_question`、`platform_security_events`とsentinel 1件を復元後に照合した。

## 3. 改ざん検知証跡

- DR canonical artifact SHA-256: `sha256:5b15306b162ab41e98450b22a6d92e56d8abf73b147817cf30bae2c8f409a3cf`
- GitHub Artifact ID: `9546985315`
- Artifact name: `stage6r10-tokyo-ishikari-dr-evidence-32802344728-1`
- GitHub Artifact digest: `sha256:29e348d95888ce1440069ab5c22fcbf0d63dff63f9d637835f53121c590d5477`

前者はcanonical DR JSONの内容完全性、後者はCIが収集した証跡ZIP全体の同一性を検証する。SHA-256は電子署名や実行主体の本人性を単独で保証しない。

## 4. RED→GREEN履歴

| Run | head | 結果 | 採否 |
|---:|---|---|---|
| #1 `32801014338` | `14a0a05c` | 84/85、TC-078だけRED | 期待REDとして採用 |
| #2 `32801652909` | `a712ac0b` | native artifact accepted、`false`を扱うテスト式の欠陥 | 不採用、テスト修正 |
| #3 `32801953471` | `1394fe61` | 85/85だがTC-030でrunner依存コマンド欠落を再監査で検出 | 最終受入には不採用 |
| #4 `32802344728` | `818a1755` | 85/85、TC-030実走査、コマンド欠落なし | 正式受入 |

Run #2と#3を成功証跡へ読み替えず、テスト自体の完全性を修正してからRun #4を受入正本とした。

## 5. 受入境界

本証跡は二つの独立したnative PostgreSQLプロセスを石狩primary役・東京recovery役として実行した結果である。さくらのクラウドの物理リージョン、Object Storage CRR、GSLB、実秘密管理基盤、外部スマートフォンからの到達性は操作しておらず、これらの実環境受入を代替しない。
