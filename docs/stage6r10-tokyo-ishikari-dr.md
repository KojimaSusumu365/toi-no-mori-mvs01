# Stage 6R-10 東京–石狩DR切替・復旧証跡 RED→GREEN仕様書

- 文書ID: QF-ST6R10-MVS01-001
- 版: Version 0.2
- 日付: 2026-08-25
- 入力基準: ADR-0003、ADR-0007 D5、ADR-0008 D3
- 対象試験: `TC-ACC-MVS01-078-DR`
- 現在判定: **ACCEPTED / 85 OF 85 GREEN**

## 1. 目的

石狩を通常時primary、東京を災害時recoveryとする決定を、暗号化backupの復元だけで終わらせず、安全な切替順序と改ざん検知可能な証跡までnative試験へ接続する。復旧先には最新migration、tenant複合外部キー、platform監査表・データが復元されなければならない。

本Stageは二つの独立PostgreSQLプロセスと別data directoryを使う実行試験である。地理的リージョン、Object Storage CRR、GSLB APIは操作しない。したがって証跡は`isSimulated=false`（mockでないnative実行）と同時に`physicalRegionFailover=false`を持ち、`measurementScope`で測定範囲を限定する。

## 2. V字の仕様・試験対

| 左側仕様 | 失敗先行試験 | GREEN実装 | 受入条件 |
|---|---|---|---|
| 石狩write経路の先行隔離 | DR TC-078 | primary PostgreSQL停止確認 | 東京write有効化より前に停止済み |
| 二者承認 | DR TC-078 | Incident Commander / Recovery Leadの異subject検証 | 同一subjectの二重承認を拒否 |
| 最新schemaの隔離復元 | DR TC-078 | restore report v2 | migration 005、複合FK、platform監査表を確認 |
| platform監査の復旧 | DR TC-078 | backup内へsentinel監査を含める | 復旧後countとtable存在を確認 |
| 安全な経路切替 | DR TC-078 | 時系列をfail-closed検証 | 隔離→復元→受入→切替の順序 |
| 不変証跡 | DR TC-078 | canonical JSON + SHA-256 seal | `artifactHash`を再計算して一致 |
| 全体回帰 | Stage 6R-10 CI契約 | 非root native wrapper | DR 5、全85件 |

## 3. 切替状態契約

1. backup snapshotを生成し、暗号化・署名・内部SHA-256を検証する。
2. 災害宣言UTCを記録する。
3. 石狩primary役のAPIを停止し、PostgreSQLの停止を`pg_ctl status`で確認する。
4. 東京recovery役の空data directoryへ復元する。
5. 公開sentinel、tenant監査、platform監査、最新schemaを照合する。
6. Incident CommanderとRecovery Leadの異なるsubjectが復旧と切替を承認する。
7. 上記合格後だけ論理routeを東京へ切り替えた証跡を確定する。
8. source/recoveryを同時にwrite primaryとしない。

時刻は`disasterDeclaredAtUtc <= sourceWriteIsolatedAtUtc <= recoveryRestoreStartedAtUtc <= recoveryRestoreCompletedAtUtc <= recoveryAcceptedAtUtc <= routeSwitchedAtUtc`を満たすこと。同一秒は許容するが逆転は拒否する。

## 4. 復元schema契約

復旧reportは次を機械可読で返す。

- `latestMigrationVersion`が`005_stage6r7_append_only.sql`で終わる。
- `fk_published_revision_same_question`が存在し、validatedである。
- `platform_security_events`が存在する。
- platform監査sentinelが1件復元される。

件数だけでは同名の古いschemaを誤認できるため、migration名・constraint・tableを対で検査する。

## 5. 証跡契約

`dr-failover-artifact.json`は承認、時系列、安全条件、schema契約、RPO/RTOだけを含み、credential、接続文字列、鍵、token、Cookie、問い本文を含めない。canonical JSONとして確定後、そのSHA-256を`dr-failover-evidence.json`の`artifactHash`へ保存する。

証跡sealの必須属性:

- `isSimulated=false`
- `measurementScope=native-local-dual-cluster-role-drill`
- `physicalRegionFailover=false`
- `artifactHash=sha256:<64 hex>`
- `status=accepted`

## 6. 失敗先行条件（実施済み）

TC-030〜033はGREENのまま、TC-078だけをREDとする。期待する不足は次の二点である。

1. restore reportが最新schema・platform監査を報告しない。
2. 二者承認と切替時系列を検証してSHA-256封印する実装がない。

既存4件の故障や環境エラーをTC-078のREDと読み替えない。

GitHub Actions Run #1で既存84件GREEN、TC-078だけREDを確認した。これにより実装不足へ失敗原因を限定してからGREEN実装へ進んだ。

## 7. 受入gate

| Suite | 必須件数 |
|---|---:|
| Domain | 12 |
| API | 41 |
| Mobile | 7 |
| OIDC E2E | 8 |
| PostgreSQL | 12 |
| DR | 5 |
| **合計** | **85** |

Build警告0・エラー0、試験ID一意、残存failure-first contract 1/1 expected RED、非root native exact-count 85/85、immutable CI artifactを必須とする。

## 8. 受入結果

GitHub Actions Run #4（head `818a1755065dff0897620e705b9712c341d80110`）で、非root native全85件とCI構成契約をGREENとした。

- Domain 12/12、API 41/41、Mobile 7/7、OIDC E2E 8/8、PostgreSQL 12/12、DR 5/5
- TC-030で暗号化artifact内のsentinel平文不在を実走査
- TC-078でsource停止、migration 005、tenant複合FK、platform監査、異subject二者承認、時系列、SHA-256 sealを確認
- RPO 0秒、RTO 2秒（暫定上限は各3,600秒、14,400秒）
- DR内部artifact hash: `sha256:5b15306b162ab41e98450b22a6d92e56d8abf73b147817cf30bae2c8f409a3cf`
- GitHub Artifact ID: `9546985315`
- GitHub Artifact digest: `sha256:29e348d95888ce1440069ab5c22fcbf0d63dff63f9d637835f53121c590d5477`

受入までの診断履歴と不採用runを含む根拠は`docs/evidence/stage6r10-github-acceptance.md`へ固定する。

## 9. 物理リージョン受入との境界

Stage 6R-10 GREENは、手順・データ・schema・証跡のnative再現性を示す。次は別gateである。

- さくらのクラウド実アカウントでの石狩・東京resource構築
- versioningと石狩→東京CRRの到達実測
- 実秘密管理基盤の鍵分離
- GSLBの実変更と外部スマートフォンsmoke test
- 人の承認待ち、provisioning、ネットワーク時間を含む実RPO/RTO
