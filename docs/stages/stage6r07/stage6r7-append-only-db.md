# Stage 6R-7 DB追記専用・改ざん防止境界 RED→GREEN仕様書

- 文書ID: QF-ST6R7-MVS01-001
- 版: Version 0.1
- 日付: 2026-08-21
- 入力基準: ADR-0009 D9
- 対象試験: `TC-ACC-MVS01-073-PG`
- 現在判定: **ACCEPTED（Run #3、81/81 GREEN）**

## 1. 目的

`audit_events`、`platform_security_events`、`question_revisions`を追記専用データとして扱い、通常実行credentialの最小権限とDB triggerの二重境界で既存行の更新・削除を拒否する。監査履歴や公開根拠となるrevisionを上書きせず、訂正は新しい監査行または新しいrevisionとして追加する。

## 2. V字の仕様・試験対

| 左側仕様 | 失敗先行試験 | GREEN実装 | 受入条件 |
|---|---|---|---|
| tenant監査は追記専用 | applicationによるUPDATE/DELETE/TRUNCATE権限とowner操作を検査 | `prevent_audit_mutation` | 権限なし、UPDATE/DELETEはSQLSTATE 55000 |
| platform監査は追記専用 | writer/reader権限とowner操作を検査 | `prevent_platform_audit_mutation` | writerはINSERTのみ、readerはSELECTのみ |
| revisionは不変 | application権限とowner操作を検査 | `prevent_revision_mutation` | UPDATE/DELETE拒否、訂正は新revision |
| 全体回帰を正確件数で閉じる | Stage 6R-7 CI契約 | 非root native wrapper | PostgreSQL 12/12、全81/81 |

## 3. 権限境界

| credential | audit_events | platform_security_events | question_revisions |
|---|---|---|---|
| application | SELECT、INSERT | 権限なし | SELECT、INSERT |
| platform audit writer | 権限なし | INSERT | 権限なし |
| platform audit reader | 権限なし | SELECT | 権限なし |
| migration | migration適用・所有者操作 | migration適用・所有者操作 | migration適用・所有者操作 |

application、writer、readerから`UPDATE`、`DELETE`、`TRUNCATE`を明示的に剥奪する。migration credentialは通常APIから分離し、秘密管理・実行承認・監査対象とする。

## 4. trigger境界

- `audit_events`: `prevent_audit_mutation`
- `platform_security_events`: `prevent_platform_audit_mutation`
- `question_revisions`: `prevent_revision_mutation`
- すべて`BEFORE UPDATE OR DELETE FOR EACH ROW`で有効化する。
- 拒否はSQLSTATE `55000`を返す。
- 例外messageへ行データ、subject、本文、token、接続情報を含めない。
- INSERTとSELECTは既存role契約どおり継続する。

triggerはmigration ownerによる通常のUPDATE/DELETE誤操作も拒否する。一方、table ownerまたはsuperuserはtrigger無効化やDDLを実行できるため、完全なWORM媒体を意味しない。保持期限、法的削除、partition detach、break-glass操作は通常アプリから分離し、将来ADRで別途定義する。

## 5. 失敗先行条件

実装前の`TC-ACC-MVS01-073-PG`は、3 tableのtriggerが存在しないためREDでなければならない。ローカルroot環境ではPostgreSQLを起動せず、GitHub非root runnerで実DB REDを取得する。Build成功や静的契約成功をnative GREENの代替にしない。

### 5.1 GitHub失敗先行証跡

- Run: `#1` / `32437227404`
- head SHA: `9492377c250ced29af9da72eb39d78acb8b4b572`
- 結果: Domain 12/12、API 40/40、Mobile 6/6、OIDC 7/7、PostgreSQL 11/12
- RED: `TC-ACC-MVS01-073-PG`だけがtrigger欠落で失敗
- Artifact: `stage6r7-append-only-evidence-32437227404-1` / ID `9431226145`
- Artifact SHA-256: `bbdd80b02d456eb66b17dd79a880f1659b4f29e3dcb5f3065506d3fea99b9d4a`

既存11件がGREENのまま新規1件だけがREDであり、試験harnessやtoolchainの失敗ではない。

## 6. GREEN受入gate

| Suite | 必須件数 |
|---|---:|
| Domain | 12 |
| API | 40 |
| Mobile | 6 |
| OIDC E2E | 7 |
| PostgreSQL | 12 |
| DR | 4 |
| **合計** | **81** |

非root、native、Build警告0・エラー0、試験ID一意、正確件数、終了コード0、immutable artifactをすべて必須とする。

## 7. GREEN受入結果

- Run: `#3` / `32438157919`
- head SHA: `83eb08dcc93fe430a28ec13a05211c6122d0c8ce`
- 結果: Domain 12/12、API 40/40、Mobile 6/6、OIDC E2E 7/7、PostgreSQL 12/12、DR 4/4、合計81/81
- Build: warning 0 / error 0
- Artifact: `stage6r7-append-only-evidence-32438157919-1` / ID `9431515869`
- Artifact SHA-256: `041f38a9ebfc9f42557b74a5735df8b4b25857a65fbd2e9af8d50db8766440c2`

Run #2では新規TC-073-PGがGREENとなり、既存TC-075-PGのmigration台帳期待値が001〜004の4件に固定されていたことを回帰試験が検出した。期待値を001〜005の5件へ更新後、Run #3でPostgreSQL 12/12と全81/81を確定した。
