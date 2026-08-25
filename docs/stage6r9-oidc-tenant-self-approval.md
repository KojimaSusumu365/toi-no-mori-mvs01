# Stage 6R-9 実OIDC tenant mapping・自己承認境界 RED→GREEN仕様書

- 文書ID: QF-ST6R9-MVS01-001
- 版: Version 0.1
- 日付: 2026-08-25
- 入力基準: ADR-0007 D2、ADR-0008 D1
- 対象試験: `TC-ACC-MVS01-077-OIDC`
- 現在判定: **ローカル失敗先行RED確認済み**

## 1. 目的

署名・issuer・audience・nonce・PKCEを検証したOIDC ID tokenの外部組織claimを、BFF Cookie発行前にissuer付き許可表で内部tenant UUIDへ変換する。未登録組織にはsessionを発行しない。EditorとReviewerの両roleを持つ利用者でも、問いの所有者と同じ`sub`なら正しいETagを提示しても自己承認を拒否する。

本Stageの「実OIDC」は、試験用ヘッダーではなく、独立HTTPS試験IdPとのdiscovery、authorization code、PKCE、token、JWKS署名、nonce、Cookieを往復するnative E2Eを意味する。実Entra tenant接続は別gateである。

## 2. V字の仕様・試験対

| 左側仕様 | 失敗先行試験 | GREEN実装 | 受入条件 |
|---|---|---|---|
| issuer付き外部組織許可表 | OIDC TC-077 | token検証時に`TenantResolver`で変換 | 登録組織だけが内部tenantを得る |
| Cookie発行前fail-closed | OIDC TC-077 | 未登録組織で`context.Fail` | 汎用failure表示、BFF sessionなし |
| session tenant固定 | OIDC TC-077 | 内部tenant claimを生成し外部claimを除去 | endpointは内部UUIDだけでscopeを決定 |
| dual-role自己承認禁止 | OIDC TC-077 | Domainのowner/reviewer `sub`比較を維持 | strong `If-Match`一致でも403 |
| 異なるReviewer承認 | OIDC TC-077 | 同一tenant・異`sub`のReviewer | 同じ詳細ETagで200・公開可能 |
| 全体回帰 | Stage 6R-9 CI契約 | 非root native wrapper | OIDC 8、全84件 |

## 3. tenant mapping契約

1. ID tokenの署名、issuer、audience、有効期限、nonce、`auth_time`を検証する。
2. 検証済みtokenから`external_organization_id`を正確に1件取得する。
3. `(verified issuer, external organization ID)`を設定済み許可表で内部tenant UUIDへ変換する。
4. 欠落、複数、空、未登録はremote authentication failureとし、Cookieを発行しない。
5. IdPから注入された`internal_tenant_id`や`verified_issuer`を信用せず除去する。
6. BFF Cookieにはサーバーが生成した内部tenant claimだけを保持し、外部組織claimを保持しない。

## 4. 自己承認契約

1. `Editor,Reviewer`両roleの同一OIDC subjectが問いを作成・申請できる。
2. 審査詳細GETのstrong ETagを承認`If-Match`に使用する。
3. roleがReviewerでも`ownerSubject == reviewer sub`なら403とする。
4. 同一tenantへmappingされた別subjectのReviewerは、同じ審査詳細ETagで承認できる。

roleは操作資格、`sub`は本人性であり、両者を同一視しない。

## 5. 受入gate

| Suite | 必須件数 |
|---|---:|
| Domain | 12 |
| API | 41 |
| Mobile | 7 |
| OIDC E2E | 8 |
| PostgreSQL | 12 |
| DR | 4 |
| **合計** | **84** |

Build警告0・エラー0、試験ID一意、残存failure-first contract 2/2 expected RED、非root native exact-count 84/84、immutable artifactを必須とする。

## 6. 失敗先行判定

- 既存OIDC 7件はGREEN。
- 新規TC-077は、未登録組織でもOIDC loginが成功してCookie発行へ進むためRED。
- dual-role同一`sub`のstrong ETag自己承認は既存Domain境界により403となる。
- REDはtenant mappingをCookie発行前へ移す不足だけを示し、既存OIDC protocolや自己承認機能の故障ではない。
