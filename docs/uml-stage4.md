# QF-UML-MVS01-004 スマートフォン/BFF UML仕様書

- Version: 0.4
- 日付: 2026-08-16
- 対象: MVS-01 Stage 4

以下は実装、要求、試験と同じ変更で維持するPlantUML原本である。`implemented`は本リポジトリで自動検証済み、`planned IdP/cloud`は実契約環境での接続・訓練待ちを表す。

## UML-DPL-MVS01-004 スマートフォン・BFF・認証配置

```plantuml
@startuml
left to right direction
skinparam componentStyle rectangle

node "Smartphone browser\n360 CSS px target" as Phone {
  component "HTML / CSS / JS\nno token storage" as Ui
  component "HttpOnly host Cookie" as BrowserCookie
}

cloud "Sakura GSLB / Load Balancer\nplanned cloud" as Edge

node "Ishikari API tier\n2 instances target" as ApiTier {
  component "Static /app\nimplemented" as Static
  component "BFF /bff\nimplemented" as Bff
  component "Minimal API /api\nimplemented" as Api
}

database "Ishikari PostgreSQL\nprivate network" as Db
folder "Shared Data Protection key ring\nencrypted at rest" as KeyRing
component "Data Protection certificate\nsecret injection" as DpCert
cloud "OIDC identity provider\nMFA + role claims\nplanned IdP" as Idp

Ui --> Edge : HTTPS same origin
Edge --> Static : GET /app/*
Edge --> Bff : /bff login/session/logout
Edge --> Api : public/admin API
Bff --> Idp : code + PKCE, server channel
Bff --> BrowserCookie : encrypted session only
Api --> Db : PostgreSQL TLS VerifyFull
Bff --> KeyRing : protect/unprotect Cookie
KeyRing --> DpCert : encrypt keys at rest
@enduml
```

配置制約:

- access token、refresh token、client secretをスマートフォンへ配らない。
- UI、BFF、APIは同一originとし、CORSを不要にする。
- API instance間でkey ringを共有し、key ringはcertificateで暗号化する。
- PostgreSQL、key ring、PFXをInternetへ公開しない。
- Load BalancerでTLS終端する場合は、既知proxyだけを信頼するforwarded headers設定を本番化前に追加する。

## UML-SEQ-MVS01-010 OIDCログインとBFF session（TC-034〜038）

```plantuml
@startuml
actor User
participant "Smartphone UI" as Ui
participant "ASP.NET BFF" as Bff
participant "OIDC provider" as Idp
participant "Data Protection" as Dp

User -> Ui : 管理者ログイン
Ui -> Bff : GET /bff/login?returnUrl=/app/
Bff -> Bff : return URLを/app配下へ正規化
Bff -> Bff : PKCE verifier/challenge生成
Bff -> Idp : Authorization request + challenge
Idp -> User : password + MFA
User -> Idp : MFA完了
Idp --> Bff : callback: code
Bff -> Idp : code + verifier + client authentication
Idp --> Bff : 検証対象token（server side）
Bff -> Bff : issuer/sub/amr=mfa/role検証
Bff -> Bff : random CSRF claim生成
Bff -> Dp : session ticket暗号化
Bff --> Ui : __Host HttpOnly Secure Cookie
Ui -> Bff : GET /bff/session + Cookie
Bff -> Dp : session ticket復号
Bff --> Ui : subject/name/roles/csrfToken only + no-store
note over Ui
OIDC tokenは受領・保存しない
end note
@enduml
```

異常系:

- ProductionでOIDC無効、AuthorityがHTTP、client secret欠落なら起動を拒否する。
- `sub`がないtoken、`amr=mfa`がない管理要求、必要roleがない操作を拒否する。
- 外部return URLは`/app/`へ置き換える。

## UML-SEQ-MVS01-011 CSRF付き下書き作成（TC-036、037、041、042）

```plantuml
@startuml
actor Editor
participant "Smartphone UI" as Ui
participant "BFF session" as Bff
participant "Admin API" as Api
participant "CSRF filter" as Csrf
database "Question store" as Store

Editor -> Ui : 下書き入力
Ui -> Bff : GET /bff/session + HttpOnly Cookie
Bff --> Ui : displayName/roles/csrfToken
Ui -> Api : POST /api/admin/questions\nCookie + X-CSRF-Token + JSON
Api -> Api : authenticated + amr=mfa + Editor
Api -> Csrf : header tokenとsession claim
Csrf -> Csrf : fixed-time comparison
Csrf --> Api : valid
Api -> Store : create DRAFT + audit
Store --> Api : question + version
Api --> Ui : 201 JSON + ETag
Ui -> Ui : textContentで結果表示
@enduml
```

拒否条件:

- Cookie、MFA claim、Editor role、CSRF tokenのいずれかがなければ保存しない。
- 本文をHTMLとして挿入せず、公開検索結果も`textContent`で描画する。
- BFF/admin/app shellを共有cacheへ保存させない。

## UML-TST-MVS01-004 V字対応

```plantuml
@startuml
left to right direction
rectangle "REQ-IAM-002\nOIDC required" as Iam2
rectangle "REQ-IAM-003\nMFA required" as Iam3
rectangle "REQ-SEC-003\nBFF/Cookie/CSRF" as Sec3
rectangle "REQ-SEC-004\nBrowser containment" as Sec4
rectangle "REQ-MOB-001/002\n360px/accessibility" as Mobile
rectangle "ADR-0004\nUML-DPL/SEQ" as Design
rectangle "ASP.NET BFF + /app" as Code
rectangle "API TC-034..038" as ApiTests
rectangle "Mobile TC-039..042" as MobileTests

Iam2 --> Design
Iam3 --> Design
Sec3 --> Design
Sec4 --> Design
Mobile --> Design
Design --> Code
Code --> ApiTests
Code --> MobileTests
ApiTests --> Iam2 : verifies
ApiTests --> Iam3 : verifies
ApiTests --> Sec3 : verifies
MobileTests --> Sec4 : verifies
MobileTests --> Mobile : verifies
@enduml
```

Stage 4時点のローカル完了条件は、要求ID、ADR、UML ID、BFF/UI実装、TC-034〜042の対応が切れておらず、当時の全46件が合格することだった。Stage 5追加後の現行gateは`uml-stage5.md`と`verification-result.md`に記録した全52件である。Production運用開始には、実IdP/MFA、実スマートフォン、さくらの外部HTTPS経路、proxy trust、鍵保管を使う受入試験を追加で必要とする。
