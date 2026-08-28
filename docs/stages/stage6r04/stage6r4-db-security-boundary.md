# Stage 6R-4 DBセキュリティ境界 失敗先行→ロール分離

- 文書ID: QF-ST6R4DB-MVS01-001
- 版: Version 0.2
- 日付: 2026-08-20
- 対応: ADR-0007 D1/D3、TC-ACC-MVS01-066-API/PG
- 判定: **実装GREEN、実PostgreSQL未実行のため受入未完了**

## 1. 失敗先行

TC-066-APIを追加し、Production構成でmigration用接続がない場合に起動を拒否する契約を固定した。製品変更前は既存API 35件が合格し、新規1件だけが次の理由で失敗した。

```text
Production must reject a PostgreSQL configuration
without a separate migration credential.
Expected InvalidOperationException, but no exception was thrown.
```

## 2. 実装した境界

| 項目 | applicationロール | migrationロール |
|---|---|---|
| 接続設定 | `ConnectionStrings:PostgreSql` | `ConnectionStrings:PostgreSqlMigrator` |
| username | migrationと異なること | applicationと異なること |
| Production TLS | `VerifyFull`必須 | `VerifyFull`必須 |
| schema DDL | 不可 | 管理schemaのowner/DDL担当 |
| `questions` | `SELECT/INSERT/UPDATE` | ownerとしてmigrationを実行 |
| `question_revisions` | `SELECT/INSERT` | ownerとしてmigrationを実行 |
| `idempotency_records` | `SELECT/INSERT/DELETE` | ownerとしてmigrationを実行 |
| `audit_events` | `SELECT/INSERT` | ownerとしてmigrationを実行 |
| `tenants` | `SELECT`のみ | owner |
| `schema_migrations` | 権限なし | 読書き |
| RLS回避 | superuser、owner、`BYPASSRLS`を禁止 | application接続には使用禁止 |

`PostgreSqlRoleBoundaryValidator`はmigration完了後、application資格情報そのものでcatalogを検査する。`NOINHERIT`、非superuser、非`BYPASSRLS`、非owner、schema `CREATE`なし、必要DMLあり、過剰権限なし、migration ledger権限なしの全条件が成立しなければ起動を中止する。診断を無効化する設定は設けない。

## 3. 右側試験

### TC-066-API

- migration接続欠落を拒否する。
- application/migration username共用を拒否する。
- migration接続の`VerifyFull`欠落を拒否する。
- 実装後のAPI suiteは36/36 GREEN。

### TC-066-PG

- applicationロールが`NOINHERIT`、非superuser、非`BYPASSRLS`、非ownerである。
- tenant表の必要DMLだけを持ち、schema DDLとmigration ledger参照を持たない。
- protected table 4表のownerが別migrationロールである。
- superuser候補、table-owner候補、`BYPASSRLS`候補を起動診断が拒否する。
- transaction-local tenant設定がcommit後に消える。

TC-067/068/074/075-PGは、RLS越境防止、複合FK、tenant単位冪等性、001→002→003移行を分離ロール構成で再確認する。

## 4. 実DB実行結果

`./scripts/test-postgresql.sh`はPostgreSQL 18.6と更新済みtest assemblyを確認し、buildを警告0・エラー0で完了した。その後、このWork環境がrootから`nobody`への実効UID変更を許可しないためinitdb前にexit 2で停止した。PostgreSQLのroot拒否やrunner guardは解除していない。

したがって次を区別する。

- ロール分離コード・構成試験: GREEN
- PostgreSQL native 10件: build済み、実行0件
- Stage 6R-4 DB受入: 未完了

## 5. 非root CI

Stage 6R-4Cで`.github/workflows/stage6r4c-nonroot-postgresql.yml`を追加した。Ubuntu 24.04 runnerで非rootを明示検査し、固定checksumの.NET 10.0.400/PostgreSQL 18.6を構築した後、この必須gateを実行する。外部actionはcommit SHAへ固定し、repository権限はread-only、checkout credential非保持、`pull_request_target`と`sudo`は不使用とした。

CI構成契約は6/6合格し、このWork環境ではroot時にnative suiteを開始せずexit 2となる失敗閉鎖を確認した。実際のGitHub Actions runはまだ行っていないため、PostgreSQL 10/10は未合格のままである。詳細は`docs/stages/stage6r04/stage6r4c-nonroot-postgresql-ci.md`を参照する。

## 6. 再実行条件

非root processを起動できるLinux/CIで次を実行する。

```bash
POSTGRES_BIN_DIR=/path/to/postgresql/bin ./scripts/test-stage6r4-db-security.sh
```

この必須gateはAPI 36件とPostgreSQL 10件を連続実行する。runnerは一時clusterへadmin、`mvs01_migrator`、`mvs01_app`、`mvs01_bypass_test`を作る。実測10件がすべて合格し、終了コード0と証跡を得た時点でだけ「PostgreSQL実DB GREEN」と判定する。

## 7. 次のV字gate

1. 非root CIでPostgreSQL 10/10を得る。
2. DR runnerへ同じロール分離を適用し、DR 4件を再実行する。
3. Stage 6R-5のplatform security audit、Auditor API、監査改ざん防止へ進む。

ローカル構成試験のGREENを、本番DB・さくら実環境・災害復旧の受入へ読み替えてはならない。
