# Stage 6R-4 テナント境界 赤→緑 仕様・実施記録

- 文書ID: QF-ST6R4-MVS01-001
- 版: Version 0.2
- 日付: 2026-08-20
- 入力基準: 承認済みADR-0007 D1〜D4、ADR-0008 D5、RV-021/RV-023/RVA-C05/C06
- 判定: **API・DB構成境界はGREEN。PostgreSQL native 10件は実DB未実行のため未合格**

## 1. 反復の境界

本反復は、外部組織claimから内部tenant UUIDへの変換、管理APIのtenant伝搬、存在秘匿404、PostgreSQLのtenant列・RLS・同一tenant複合外部キーを一続きの縦切りとして扱う。resolverだけ、SQLだけを単独で合格にはしない。

今回native試験へ移した契約は次の7件である。

| ID | 層 | 状態 |
|---|---|---|
| TC-ACC-MVS01-065-API | API | GREEN |
| TC-ACC-MVS01-066-API | API/構成 | GREEN |
| TC-ACC-MVS01-069-API | API | GREEN |
| TC-ACC-MVS01-066-PG | PostgreSQL | 実装・build済み、実DB未実行 |
| TC-ACC-MVS01-067-PG | PostgreSQL | 実装・build済み、実DB未実行 |
| TC-ACC-MVS01-068-PG | PostgreSQL | 実装・build済み、実DB未実行 |
| TC-ACC-MVS01-074-PG | PostgreSQL | 実装・build済み、実DB未実行 |
| TC-ACC-MVS01-075-PG | PostgreSQL | 実装・build済み、実DB未実行 |

## 2. 認証・tenant解決契約

管理APIは、署名検証済みprincipalの`iss`と`external_organization_id`を読む。値をUUIDとして直接信用せず、`Tenancy:Organizations:<entry>:{Issuer,ExternalOrganizationId,InternalTenantId}`の許可表だけで内部UUIDへ変換する。

| 条件 | 応答 | 業務Store |
|---|---|---|
| claim欠落・空 | 403、`tenant-claim-missing` | 呼ばない |
| issuer欠落/不一致・複数claim・未登録値 | 403、`tenant-claim-invalid-or-unmapped` | 呼ばない |
| 許可表に完全一致 | 内部UUIDを`HttpContext.Items`へ格納 | tenant付きで呼ぶ |

外部組織ID、内部tenant UUID、claim全文は応答へ含めない。拒否本文へ入力値を反射しない。tenant未確定拒否の専用platform監査はADR-0010に従い次反復へ残し、通常tenant監査へ誤って書かない。

## 3. 可視性と404

- Editorは同一tenant内でも自分が所有する問いだけを管理できる。
- Reviewerは同一tenant内だけを管理できる。
- 他所有者、他tenant、実在しないIDは同じ`resource-not-visible-or-missing` Problem Details 404とする。
- title/type/statusを統一し、対象IDやtenant情報を本文へ含めない。
- 公開APIは現時点で移行tenant `TenantIds.Mvs01`に固定する。複数公開tenantのhost/path解決は本反復外である。

## 4. StoreとPostgreSQL契約

全管理Store操作は`tenantId`を必須引数に持つ。InMemoryは辞書検索・一覧・監査・冪等キーをtenantで分離する。PostgreSQLは各業務操作を次の形で実行する。

1. connectionを開く。
2. transactionを開始する。
3. `set_config('app.tenant_id', internalTenantId, true)`を実行する。
4. SQLの`tenant_id = @tenant_id`条件とRLSの両方を通す。
5. commit/rollbackでtenant設定を消去する。

第三引数`true`を外してsession設定にしてはならない。connection poolへtenant状態を持ち越さない。

## 5. Expand/Contract migration

`002_stage6r_expand.sql`は既存Stage 6データへ固定移行tenantを付け、`question_revisions`、承認・取下げ列、tenant付き監査・冪等列を作る。`003_stage6r_contract.sql`は一時defaultを削除し、次の4表でRLSをENABLEかつFORCEする。

- `questions`
- `question_revisions`
- `idempotency_records`
- `audit_events`

`question_revisions`は`(tenant_id, question_id)`で`questions`へ結び、`questions.published_revision_id`は`(tenant_id, id, published_revision_id)`で同じ問いのrevisionだけを参照できる。冪等キーの主キーは`(tenant_id, idempotency_key)`である。

## 6. 赤→緑

製品変更前のnative API結果は33件合格・新規2件失敗だった。

- TC-065: claim欠落でも201 Created。
- TC-069: 他所有者更新が403で、他tenant 404と応答差がある。

tenant境界実装後はAPI 35/35。追加確認で404本文差を検出したため、他所有者・他tenantを同じ正規化Problem Detailsへ修正し、type/title一致とID非開示を試験へ追加した。

DBロール反復ではTC-066-APIを追加し、既存35件合格・新規1件失敗を先に確認した。失敗内容は、migration接続が存在しなくてもProduction構成を生成できることだった。二つの接続、異なるusername、双方の`VerifyFull`を強制した後、API 36/36へGREEN化した。

PostgreSQL native suiteは既存5件に新規5件を加えた10件としてbuild済みである。ただしWork環境はrootから非rootへの実効UID変更を禁止し、PostgreSQL自身もroot起動を拒否する。`./scripts/test-postgresql.sh`は安全guardを維持したままexit 2で停止した。この5件をGREENとは数えない。

`AppHost`はapplication/migration用の異なるusernameを必須にし、別々の`NpgsqlDataSource`を注入する。migration側は管理schemaへMigration 001〜003を適用後、application側へ必要最小DMLを付与する。application側は`NOINHERIT`、非owner、非superuser、非`BYPASSRLS`、schema `CREATE`なし、migration ledger権限なしをcatalogで診断し、逸脱時は起動を中止する。TC-066-PGには安全ロールの成功だけでなく、superuser、table-owner、`BYPASSRLS`候補の拒否も組み込んだ。

ただしWork環境の実効UID制約は継続しており、更新後の実DB suiteもinitdb前にexit 2で停止した。ロール分離の「実装GREEN」とPostgreSQLの「実行GREEN」を混同しない。

## 7. V字判定

| 左側仕様 | 実装 | 右側試験 | 判定 |
|---|---|---|---|
| ADR-0007 D2 | `TenantResolver`、`RequireTenantFilter` | TC-065-API | GREEN |
| ADR-0007 D1/D3 構成 | 2接続、異role、双方VerifyFull | TC-066-API | GREEN |
| RV-021 | tenant/owner不可視と正規化404 | TC-069-API | GREEN |
| ADR-0007 D1/D3 | tenant列、transaction-local設定、強制RLS | TC-066/067-PG | 未実行 |
| ADR-0007 D4 | 同一tenant・同一question複合FK | TC-068-PG | 未実行 |
| ADR-0008 D5/RV-023 | tenant付き冪等scope・期限 | TC-074-PG | 未実行 |
| Migration 002/003 | Expand/Contract、default撤去、revision | TC-075-PG | 未実行 |

## 8. 再実行

```bash
./scripts/test-stage6r4-tenant-boundary.sh
MVS01_RUN_POSTGRESQL=1 ./scripts/test-stage6r4-tenant-boundary.sh
```

1行目はAPI GREEN、ID一意性、残存11件の期待REDを確認する部分gateである。2行目は非root PostgreSQLを起動できる環境で実DB10件まで実行する。実DBを走らせない1行目だけでStage 6R-4全体合格としてはならない。

## 9. 次gate

- 非root PostgreSQL runnerまたはCIで、更新後のapplication/migration/BYPASSRLS試験ロールを生成してnative 10件を実行する。
- Stage 6R-5でcorrelation/request ID分離、拒否監査envelope、platform security events、Auditor APIを失敗先行で組み込む。
- 実Entra tokenで組織claim mapping、MFA、role、自己承認をTC-077-OIDCとして受け入れる。

本成果物は本番候補ではない。DB実行、DBロール実環境確認、platform監査、実IdP、実端末、DR、実クラウドのgateが残る。
