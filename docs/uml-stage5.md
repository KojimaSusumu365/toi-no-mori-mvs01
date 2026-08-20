# QF-UML-MVS01-005 OIDC実プロトコル UML仕様書

- Version: 0.5
- 日付: 2026-08-16
- 対象: MVS-01 Stage 5

以下は実装、要求、試験と同じ変更で維持するPlantUML原本である。試験IdPはtest assembly内だけに存在し、Production publishへ含めない。

## UML-SEQ-MVS01-012 OIDC code + PKCE実往復（TC-043）

```plantuml
@startuml
actor Editor
participant "HTTPS browser simulator" as Browser
participant "ASP.NET BFF" as Bff
participant "OIDC provider" as Idp
participant "JWKS" as Jwks

Editor -> Browser : select administrator login
Browser -> Bff : GET /bff/login
Bff -> Idp : authorize + state + nonce + PKCE challenge + max_age=900
Idp --> Browser : redirect code + state
Browser -> Bff : GET /signin-oidc + code + state
Bff -> Idp : POST /token + secret + code + PKCE verifier
Idp -> Idp : one-time code and PKCE validation
Idp --> Bff : signed ID token + server-side access token
Bff -> Jwks : retrieve RSA public key
Jwks --> Bff : matching kid/key
Bff -> Bff : issuer/audience/signature/nonce/sub/auth_time validation
Bff --> Browser : Secure HttpOnly Cookie only
Browser -> Bff : GET /bff/session + Cookie
Bff --> Browser : subject/name/roles/CSRF only
@enduml
```

事後条件:

- access token、ID token、client secret、PKCE verifierはbrowser CookieまたはBFF session JSONへ入らない。
- `roles=Editor`と`amr=mfa`を別々に検査する。
- `auth_time`は現在から15分＋clock skew 1分以内とする。

## UML-SEQ-MVS01-013 安全側拒否（TC-044、045、048）

```plantuml
@startuml
participant Browser
participant BFF
participant OIDCProvider
participant Policy

Browser -> BFF : explicit GET /bff/login
BFF -> OIDCProvider : authorization code flow
OIDCProvider --> BFF : signed identity response
BFF -> BFF : signature and auth_time validation
BFF -> Policy : MFA claim and role validation
Policy --> BFF : reject invalid signature, missing MFA, or stale auth_time
BFF --> Browser : generic /app authentication=failed or 403
Browser -> BFF : GET /bff/session
BFF --> Browser : 401 or 403, no automatic OIDC redirect
@enduml
```

拒否理由のtoken内容、鍵情報、exceptionをbrowserへ出さない。signature/auth_time不正はsession Cookieを作成せず、MFA claim欠落は認証済みでも管理authorizationを403にする。

## UML-SEQ-MVS01-014 CSRF更新とlogout（TC-046、047）

```plantuml
@startuml
actor Editor
participant Browser
participant BFF
participant AdminAPI
participant OIDCProvider

Editor -> Browser : create draft
Browser -> BFF : GET /bff/session + Cookie
BFF --> Browser : login-bound CSRF
Browser -> AdminAPI : POST draft without CSRF
AdminAPI --> Browser : 403
Browser -> AdminAPI : POST draft + Cookie + CSRF
AdminAPI --> Browser : 201 DRAFT
Editor -> Browser : logout
Browser -> BFF : POST /bff/logout + CSRF
BFF -> OIDCProvider : end-session
OIDCProvider --> BFF : signed-out callback
BFF --> Browser : expire local Cookie and return /app
Browser -> BFF : GET /bff/session
BFF --> Browser : 401
@enduml
```

## UML-TST-MVS01-005 V字対応

```plantuml
@startuml
left to right direction
rectangle "REQ-IAM-004\nOIDC protocol session" as Requirement1
rectangle "REQ-IAM-003\nMFA evidence" as Requirement2
rectangle "REQ-SEC-005\nSignature and recency" as Requirement3
rectangle "ADR-0005\nUML-SEQ-012..014" as Design
rectangle "BFF + test OIDC provider" as Code
rectangle "TC-043/047/048" as ProtocolTests
rectangle "TC-044/045" as NegativeTests
rectangle "TC-046" as CsrfTest

Requirement1 --> Design
Requirement2 --> Design
Requirement3 --> Design
Design --> Code
Code --> ProtocolTests
Code --> NegativeTests
Code --> CsrfTest
ProtocolTests --> Requirement1 : verifies
NegativeTests --> Requirement2 : verifies
NegativeTests --> Requirement3 : verifies
CsrfTest --> Requirement3 : verifies
@enduml
```

Stage 5ローカル完了条件は、Stage 4の全46件を維持し、OIDC E2E TC-043〜048を加えた全52件が合格することである。実IdP gateは`entra-id-setup.md`のENTRA-AT-01〜10を別途満たす必要がある。

