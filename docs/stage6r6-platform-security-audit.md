# Stage 6R-6 Platform Security監査境界 失敗先行→GREEN仕様書

- 文書ID: QF-ST6R6-MVS01-001
- 版: Version 0.2
- 日付: 2026-08-21
- 入力基準: ADR-0009 D1/D5/D6/D8、ADR-0010 D1/D2
- 判定: **受入完了。GitHub非root native 80/80 GREEN**

## 1. 目的

tenant内の業務監査`audit_events`と、tenantを確定できない拒否を含む`platform_security_events`を分離する。拒否監査のために元の401/403/404/429を遅延・変更させず、429攻撃を監査DBへの1要求1書込みへ増幅させない。

Stage 6R-5で確定したtenant Auditorはplatform監査を読めない。PlatformAuditorは内部tenant contextを使わない期間必須の専用APIだけを利用する。

## 2. V字の仕様・試験対

| 左側仕様 | 実装 | 右側native試験 | 状態 |
|---|---|---|---|
| 相関単位と要求単位を分ける | `CorrelationContextMiddleware`、`X-Correlation-ID`、`X-Request-ID` | TC-ACC-MVS01-070-API | 37/40 REDから40/40 GREEN |
| 拒否metadataだけをplatform監査へ送る | `AccessDenialAuditEnvelope`、正規化action、HMAC partition | TC-ACC-MVS01-071-API | GREEN |
| 429をUTC 1分窓で抑制する | bounded queue、process内抑制、DB部分一意索引 | TC-ACC-MVS01-071-API | GREEN |
| 監査sink障害で元応答を変えない | 非同期worker、timeout、fallback metric/log | TC-ACC-MVS01-080-API | GREEN |
| platform表とcredentialを分離する | migration 004、writer INSERT-only、reader SELECT-only | TC-ACC-MVS01-071-PG | GitHub非root PostgreSQL 11/11 GREEN |

## 3. HTTP契約

### 3.1 応答追跡header

- 安全な`X-Correlation-ID`は複数要求を束ねる値として維持する。
- 不正・空・64文字超の値はサーバー生成値へ置換する。
- `X-Request-ID`は要求ごとに新規生成し、同じcorrelation内でも再利用しない。
- 両値に改行、空白、URL、tokenを許可しない。

### 3.2 PlatformAuditor API

`GET /api/platform/security-events?from=<UTC>&to=<UTC>&limit=<1..200>`

- `PlatformAuditor`、MFA、認証済みsubjectを必須とする。
- `from`と`to`を必須とし、`from < to`、最大31日とする。
- tenant Auditor、Editor、Reviewerには403を返す。
- 応答は時刻、許可reason、正規化action、request ID、correlation ID、集約数だけとする。
- partition hash、内部ID、tenant、subject、生IP、claim、本文、Cookie、tokenを返さない。
- `no-store`を付与する。

## 4. 記録データの許可リスト

許可reasonは次だけである。

- `access.unauthenticated`
- `access.forbidden`
- `tenant.claim_missing`
- `tenant.claim_invalid_or_unmapped`
- `csrf.missing_or_invalid`
- `access.rate_limited`
- `resource.not_visible_or_missing`

`normalized_action`はHTTP methodとroute templateまたは固定boundaryだけを持ち、question IDやquery値を含めない。partitionはrotation可能な32byte以上の秘密を使うHMAC-SHA-256とし、生IPを保存しない。

## 5. 非同期・失敗境界

拒否応答の後処理はbounded channelへ非blocking enqueueする。queue満杯、sink timeout、DB障害では`audit_write_failures_total`を増やし、reason/request ID/correlation IDだけのfallback logを出す。例外message、接続文字列、SQL、request bodyはlogへ出さない。

429は`partition_hash + normalized_action + UTC 1-minute window`で先頭だけをqueueへ入れる。2件目以降は同期INSERTせず`security_audit_suppressed_total`を増やす。複数instance競合はDBの部分一意索引でも重複を拒否する。

## 6. PostgreSQL権限境界

| credential | 許可 | 禁止 |
|---|---|---|
| application | tenant業務表の既定最小権限 | `platform_security_events`の全権限 |
| migration | schema所有・migration・GRANT | application実行用途 |
| platform audit writer | `platform_security_events INSERT` | SELECT/UPDATE/DELETE/TRUNCATE/DDL |
| platform audit reader | `platform_security_events SELECT` | INSERT/UPDATE/DELETE/TRUNCATE/DDL |

4 roleは異なる明示username、`NOINHERIT`、非superuser、非`BYPASSRLS`とする。Productionでは全接続を`SSL Mode=VerifyFull`にする。IdPのPlatformAuditor roleとDB reader credentialは別概念であり、ブラウザや利用者へDB credentialを渡さない。

## 7. 受入gate

| Suite | 必須件数 |
|---|---:|
| Domain | 12 |
| API | 40 |
| Mobile | 6 |
| OIDC E2E | 7 |
| PostgreSQL | 11 |
| DR | 4 |
| **合計** | **80** |

非root、native、Build警告0・エラー0、試験ID一意、正確件数、終了コード0をすべて必須とする。ローカルroot環境でPostgreSQL/DRを未実行のまま80/80とは判定しない。

### 7.1 GitHub受入証跡

- Workflow: `Stage 6R-6 platform security regression`
- Run: `#1` / `32435956694`
- head SHA: `419014d5cfae3f9ff438610f46b7d7330e3fa80a`
- runner: Ubuntu 24.04、非root、native
- 結果: Domain 12/12、API 40/40、Mobile 6/6、OIDC E2E 7/7、PostgreSQL 11/11、DR 4/4、合計80/80
- Build: 警告0、エラー0
- Artifact: `stage6r6-platform-security-evidence-32435956694-1` / ID `9430807397`
- Artifact SHA-256: `b54439602551595837648a6a2c3e9c137e0d12ebe514a78460ec7891b990167d`

詳細は`docs/evidence/stage6r6-github-acceptance.md`へ固定する。

## 8. 次の小反復

TC-ACC-MVS01-073-PGとして、`audit_events`、`platform_security_events`、`question_revisions`の追記専用性をGRANTだけでなくtriggerでも固定する。Stage 6R残存6件、実Entra、実端末、さくら実リージョン復旧は別gateとする。
