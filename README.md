# 問いの森 CORE — MVS-01 Stage 6R-4C 非root PostgreSQL CI

> 外部組織claim、内部tenant UUID、管理API、Store、PostgreSQL RLS、application/migration DBロール分離を一つの縦切りへ接続した作業版です。Stage 6R-4CではGitHub Actions向け非root PostgreSQL CIを構築しました。CI構成契約は6/6 GREENですが、GitHub上のnative PostgreSQL 10/10は未実行のためDB受入は未完了です。

ASP.NET Core、PostgreSQL、スマートフォン向けWeb UI、OIDC/BFF認証境界、暗号化災害復旧を一つの縦切り実装へ接続した最小システムです。

V字工程を基本とするアジャイル方式として、要求ID、ADR/UML ID、実装、自動試験IDを同じ反復で更新します。Stage 6R-4で再実行した非DB範囲はDomain 12件、API 36件、Mobile 6件、OIDC E2E 7件の全61件です。60件が合格し、Mobile TC-055だけを承認済みAuditor仕様に対する期待REDとして残しています。

## 今回動く範囲

1. スマートフォンから承認済みの問いを検索・閲覧する。
2. 360px幅、44px操作部品、safe area、キーボード、支援技術を考慮した画面を配信する。
3. OIDC Authorization Code + PKCEでログインし、ブラウザにはHttpOnly Cookieだけを保持するBFF境界を提供する。
4. MFAとEditor roleを持つ利用者が、自分の問いを作成、一覧、版付き編集し、レビューを申請する。
5. 試験IdPと実HTTPSでdiscovery、authorization code、PKCE、token、JWKS署名、nonce、logoutを往復する。
6. 署名不正、MFA欠落、15分を超える古い認証をsession作成前またはauthorization境界で拒否する。
7. Reviewerがスマートフォン画面からレビュー待ちを確認し、理由付き差戻し、承認公開、取り下げを行う。監査画面のAuditor限定化は未実装でTC-055を赤としている。
8. 署名検証済み`external_organization_id`を許可表で内部tenant UUIDへ変換し、欠落・複数・未登録を403で拒否する。
9. Editor/Reviewerの管理操作を同一tenantへ限定し、他所有者・他tenant・不存在を同じ正規化Problem Details 404へそろえる。
10. 問い、revision、監査、冪等結果をtenant付きPostgreSQLトランザクションへ保存する実装と試験を提供する。ただし今回のWork環境では実DB試験未実行である。
11. migration/application DB接続を分離し、applicationロールを`NOINHERIT`・非owner・非superuser・非`BYPASSRLS`・DDLなしに制限して起動時に診断する。
12. 石狩本番から東京復旧へ、署名・AES-256-GCM暗号化backupを運搬する手順を検査する。

異なるEditorとReviewerによる作成、申請、承認、匿名公開までを、署名付きOIDC試験sessionで自動検証します。自己承認、他Editorの管理閲覧、古い版による上書き、CSRF欠落は安全側に拒否します。

## スマートフォン画面をローカルで確認

```bash
./scripts/run-api.sh
```

ブラウザで `http://127.0.0.1:5080/app/` を開きます。Development既定では公開検索を確認できますが、管理者ログインは安全側に無効です。本番用OIDC設定なしでテスト認証へfallbackすることはありません。

画面は同一オリジンから配信し、外部CDN、inline script/style、`localStorage`、`sessionStorage`、HTML文字列挿入APIを使いません。認証tokenやclient secretをブラウザへ返しません。

## Productionの認証契約

Productionでは次を必須とし、不足時は起動を拒否します。

- PostgreSQL接続は`SSL Mode=VerifyFull`
- application用`ConnectionStrings__PostgreSql`とmigration用`ConnectionStrings__PostgreSqlMigrator`は異なるusernameを使用する
- `Authentication__Mode=Oidc`
- HTTPSのOIDC Authority、Client ID、secret注入されたClient Secret
- `sub`、設定したMFA証跡claim、`Editor|Reviewer` roleのIdP mapping
- `external_organization_id`と`Tenancy__Organizations__<external-id>=<internal-uuid>`の明示許可表
- 署名済み`auth_time`が15分＋clock skew 1分以内
- 全API instanceで共有するData Protection key ring
- 保存keyを暗号化するprivate key付きPFXとsecret注入されたpassword

Cookieは`__Host-`、`Secure`、`HttpOnly`、`SameSite=Lax`、20分、非slidingです。管理APIはCookieに加えてMFA、role、login-bound CSRF token、許可表で解決した内部tenant UUIDを検査します。未認証APIは401とし、OIDC loginは`/bff/login`からだけ開始します。外部組織IDをUUIDとして直接信用したり、応答へ返したりしません。

初期managed IdPはMicrosoft Entra ID workforce tenantを第一候補とし、app roleの`roles` claimを使用します。ただし実tenant tokenのMFA証跡はまだ未確認です。`amr=mfa`を仮定して検査を外すことはせず、実tokenに合わせて設定してから受入試験します。設定例は`infra/sakura/application-config.example.env`、準備手順は`docs/entra-id-setup.md`、判断根拠は`docs/adr-0005-managed-idp-and-oidc-e2e.md`にあります。

実IdP、client credential、MFA policy、Load Balancer、共有key ring、秘密管理基盤はまだ接続していません。さくらのLoad BalancerでTLS終端する場合は、既知proxyだけを信頼するforwarded headers設定を追加してから本番化します。

## 石狩・東京の災害対策

2026年8月時点のさくらのオブジェクトストレージCRRは、石狩第1サイトから東京第1サイトへの片方向です。この制約に合わせて最重要データの本番を石狩、リージョン災害時の復旧先を東京とします。

- 石狩: API/PostgreSQL本番、署名鍵、backup worker、CRR source
- 東京: CRR destination、隔離復元先、復旧秘密鍵、Recovery API
- GSLB: 人の災害宣言、旧本番隔離、二者承認後に手動切替
- 暫定目標: RPO 1時間以内、RTO 4時間以内

自動実証済みなのはローカルの実PostgreSQLを使う暗号化・改ざん拒否・隔離復元です。さくら実契約のbucket、CRR、GSLB、東京復旧は未実施です。`docs/adr-0003-ishikari-primary-tokyo-recovery.md`と`docs/dr-runbook.md`を参照してください。

## 構成

```text
src/
  ToiNoMori.Domain/           Question集約・状態機械・不変条件
  ToiNoMori.Api/              Minimal API・BFF/OIDC・スマートフォンWeb UI
tests/
  ToiNoMori.Domain.Tests/     状態規則の単体仕様テスト
  ToiNoMori.Api.Tests/        実Kestrelを使うAPI/BFF仕様テスト
  ToiNoMori.Mobile.Tests/     360px・アクセシビリティ・CSP・配信仕様テスト
  ToiNoMori.OidcE2e.Tests/    実HTTPS・署名付き試験IdP・Cookie browser protocol E2E
  ToiNoMori.PostgreSql.Tests/ 実PostgreSQL統合試験
  ToiNoMori.Testing/          外部test frameworkに依存しない最小runner
scripts/
  check-test-ids.sh           実行suite内の試験ID重複を拒否
  test-stage6r4-tenant-boundary.sh API 36件GREEN、残存11件RED、任意PG gate
  test-stage6r4-db-security.sh  API 36件＋PostgreSQL 10件の必須gate
  test-stage6r4c-ci-contract.sh 非root CI構成・証跡判定の6件
  ci/run-stage6r4c-db-security-ci.sh CI gate実行・証跡生成
  test-all.sh                 現行native 75件（PostgreSQL/DRを含む）
  test-disaster-recovery.sh   TC-030〜033
docs/
  stage6r1-failure-first-spec.md Stage 6R-1の契約・結果・次gate
  stage6r2-domain-red-green.md   Stage 6R-2 Domain仕様・結果・次gate
  uml-stage6r2-domain.md         Domainのクラス・状態・承認・V字UML
  stage6r3-approval-api-red-green.md 承認API仕様・結果・次gate
  uml-stage6r3-approval-api.md       承認APIのcomponent・sequence・V字UML
  stage6r4-tenant-boundary-red-green.md tenant縦切り仕様・結果・次gate
  stage6r4-db-security-boundary.md      DBロール分離・起動診断・実DBgate
  stage6r4c-nonroot-postgresql-ci.md    非root GitHub Actions・証跡・受入条件
  uml-stage6r4-tenant-boundary.md       tenant解決・RLS・複合FK・V字UML
  adr-0010-*.md               platform監査分離と429抑制
  uml-stage6r1.md             監査境界とV字対応
  stage6-detailed-spec.md     Stage 6最小業務フロー詳細仕様書
  spec-test-matrix.md         要求―UML―実装―テスト追跡表
  uml-stage6.md               業務・画面・公開・差戻し・V字UML
  verification-result.md      最新の検証証跡
```

## ビルドと全テスト

.NET SDK 10.0.400とPostgreSQL 18系の`initdb`、`pg_ctl`、`pg_dump`、`pg_restore`を使用します。

プロジェクト内へ固定版を導入する場合:

```bash
./scripts/install-local-toolchain.sh
./scripts/verify-toolchain.sh
```

現在の再現環境は.NET SDK 10.0.400とPostgreSQL 18.6です。詳細は`docs/development-environment.md`を参照してください。

```bash
POSTGRES_BIN_DIR=/path/to/postgresql/bin ./scripts/test-all.sh
```

個別実行:

```bash
./scripts/check-test-ids.sh
./scripts/test-stage6r4c-ci-contract.sh
./scripts/test-stage6r4-tenant-boundary.sh # API 36件GREEN、残存11件期待RED
MVS01_RUN_POSTGRESQL=1 ./scripts/test-stage6r4-tenant-boundary.sh
./scripts/test.sh
POSTGRES_BIN_DIR=/path/to/postgresql/bin ./scripts/test-postgresql.sh
POSTGRES_BIN_DIR=/path/to/postgresql/bin \
  MVS01_DR_EVIDENCE_DIR="$PWD/docs/evidence" \
  ./scripts/test-disaster-recovery.sh
```

GitHub Actionsでは`.github/workflows/stage6r4c-nonroot-postgresql.yml`がUbuntu 24.04の非root UIDを検査し、API 36件とPostgreSQL 10件を連続実行する。結果、runner UID、commit、toolchain、log SHA-256をartifactへ保存し、件数不足や未実行を成功扱いにしない。

一件でも失敗すると終了コード1を返します。OIDC E2Eは独立HTTPS endpoint間のredirectとCookieをbrowser相当clientで往復します。今回の検証環境では非特権PostgreSQL processを起動できず、Stage 6版のPostgreSQL/DRは未再実行です。物理スマートフォンのbrowser engine描画やscreen reader操作もまだ含みません。

## 主なHTTP契約

| 操作 | メソッドと経路 | 制御 |
|---|---|---|
| Web UI | `GET /app/` | 同一origin、CSP、app shellは`no-store` |
| ログイン | `GET /bff/login` | OIDC code + PKCE、return URL制限 |
| session | `GET /bff/session` | MFA、最小応答、`no-store` |
| ログアウト | `POST /bff/logout` | MFA、CSRF |
| 管理一覧 | `GET /api/admin/questions?status=&limit=` | MFA、内部tenant、Editor本人所有またはReviewer scope |
| 管理詳細 | `GET /api/admin/questions/{id}` | MFA、内部tenant、所有者またはReviewer、ETag、不可視時は正規化404 |
| 下書き作成 | `POST /api/admin/questions` | Editor、MFA、内部tenant、CSRF |
| 下書き更新 | `PUT /api/admin/questions/{id}` | Editor、内部tenant、所有者、`If-Match`、CSRF |
| レビュー申請 | `POST /api/admin/questions/{id}/submit` | Editor、所有者、CSRF |
| 差戻し | `POST /api/admin/questions/{id}/return` | Reviewer、MFA、理由、CSRF |
| 承認 | `POST /api/admin/questions/{id}/approve` | Reviewer、自己承認禁止、strong `If-Match`、冪等、CSRF、成功時新ETag |
| 取り下げ | `POST /api/admin/questions/{id}/withdraw` | Reviewer、理由、CSRF |
| 公開検索 | `GET /api/public/questions?query=&tag=` | `PUBLISHED`のみ、rate limit |

## 次の反復

- 構築済みGitHub Actionsをrepositoryで実行し、native PostgreSQL 10/10 artifactを確認してrequired status checkへ設定する
- correlation/request ID分離、拒否監査envelope、platform security events、Auditor APIをStage 6R-5で組み込む
- 公開APIの複数tenant向けhost/path解決を設計する。現在は移行tenant MVS-01へ固定する
- 実Entra tenant/MFAを使う受入とiOS/Android実機アクセシビリティ試験
- さくらLoad Balancerのproxy trust、CRR、GSLB、東京復旧訓練
- PostgreSQL standby、WAL archive/PITR、負荷試験、容量見直し
- Infrastructure as Code、監視通知、鍵rotation、session失効運用
- Azure/AWS/GCPへの将来adapter

ローカル60件合格は本番承認ではありません。期待REDのMobile 1件・Stage 6R残存11件、未実行のPostgreSQL 10件/DR 4件、実IdP、実端末、実クラウド、運用担当者による受入とSecurity Reviewを別gateにします。
