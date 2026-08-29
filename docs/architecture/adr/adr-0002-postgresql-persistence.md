# ADR-0002 PostgreSQL永続化と安全側障害応答

- 状態: 採用
- 日付: 2026-08-16
- 対象要求: `REQ-MVS01-DAT-001`、`REQ-MVS01-DAT-002`、`REQ-MVS01-AVL-001`、`REQ-MVS01-AVL-002`

## 決定

本番の問い・監査・冪等結果はPostgreSQL 18へ永続化する。ASP.NET Coreは `IQuestionStore` にのみ依存し、開発用のメモリ実装と本番用のPostgreSQL実装を構成で切り替える。

問いの状態更新、成功監査、承認の冪等結果は一つのDBトランザクションで確定する。競合する承認再送はトランザクション単位のadvisory lockで直列化する。SQLは全てパラメータ化する。

起動時マイグレーションはDB advisory lockと `schema_migrations` で一度だけ適用する。本番環境はPostgreSQL以外のproviderを拒否し、TLS無効接続も拒否する。

Stage 6R-4追補として、`ConnectionStrings:PostgreSql`はapplicationロール、`ConnectionStrings:PostgreSqlMigrator`はmigrationロール専用とする。両者のusernameが同じ構成を起動時に拒否する。migrationロールだけが管理schemaのDDL所有者となり、applicationロールには問いの`SELECT/INSERT/UPDATE`、revisionと監査の`SELECT/INSERT`、冪等表の`SELECT/INSERT/DELETE`、`tenants`の`SELECT`、必要sequenceだけを付与する。applicationロールは`NOINHERIT`、非superuser、非`BYPASSRLS`、非table-owner、schema `CREATE`なし、`schema_migrations`参照なしを必須とし、migration後にcatalog診断して違反時は起動を中止する。

DBへ接続できない場合、readinessとデータAPIは503を返す。問題応答に接続文字列、ホスト、provider例外、SQLを含めない。livenessはプロセス生存だけを示す。

## 理由

- アプリ再起動からデータ寿命を分離できる。
- 状態と監査の片方だけが残る不整合を避けられる。
- V字工程の要求 `DAT/AVL` と試験 `TC-024〜027` を直接対応付けられる。
- 将来の複数リージョン構成でも同じアプリ契約を維持できる。
- RLSをapplicationロール自身の所有権や`BYPASSRLS`で無効化する構成事故を、Production起動時にも拒否できる。

## 限界

このADRが実証するのは単一PostgreSQLの永続化と障害時の安全側応答までである。ロール分離コードとTC-066-APIはGREENだが、TC-066-PGを含む実DB suiteは実行環境の非root process制約により未実行である。リージョン災害構成と暗号化バックアップはADR-0003、`uml-stage3.md`、DR TC-030〜033で追加した。現行CRRの片方向制約により、役割は石狩本番・東京復旧を正とする。
