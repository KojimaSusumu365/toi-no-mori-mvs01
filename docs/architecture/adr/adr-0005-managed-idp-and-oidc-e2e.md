# ADR-0005 Managed IdP候補とOIDC実プロトコルE2E

- 状態: 採用（Stage 5。実tenant接続gate待ち）
- 日付: 2026-08-16
- 対象要求: `REQ-MVS01-IAM-003`、`REQ-MVS01-IAM-004`、`REQ-MVS01-SEC-003`、`REQ-MVS01-SEC-005`
- 関連仕様: `QF-UML-MVS01-005`

## 背景

Stage 4はOIDC/BFF optionとCookie境界を検査したが、実際のauthorization endpoint、token endpoint、JWKS署名鍵、authorization code、PKCE verifier、nonce、logoutを往復していなかった。また、最重要データをさくらのクラウドへ置くことと、認証サーバーを自前運用することは同じ判断ではない。IdPのpatch、署名鍵rotation、MFA復旧、account lockout、監査を少人数で常時運用するのは重い。

初期利用者は一般公開利用者ではなく、EditorとReviewerという少数の業務担当者である。この条件ではmanaged workforce IdPが適する。

参照した公式仕様:

- Microsoft identity platform OIDC: https://learn.microsoft.com/en-us/entra/identity-platform/v2-protocols-oidc
- Microsoft Entra app roles: https://learn.microsoft.com/en-us/entra/identity-platform/howto-add-app-roles-in-apps
- Microsoft Entra claim validation: https://learn.microsoft.com/en-us/entra/identity-platform/claims-validation
- Microsoft Entra Conditional Access MFA: https://learn.microsoft.com/en-us/entra/identity/conditional-access/policy-all-users-mfa-strength
- ASP.NET Core OIDC Web authentication: https://learn.microsoft.com/en-us/aspnet/core/security/authentication/configure-oidc-web-authentication?view=aspnetcore-10.0

## 決定

### 1. IdP選択

- 初期Editor/Reviewer向けIdPの第一候補をMicrosoft Entra ID workforce tenantとする。
- 実tenant、license、運用責任者、MFA recovery手順が未決定なので、現時点ではvendor採用を最終確定しない。
- アプリはgeneric OIDCを維持し、Authority、Client ID、Client Secret、name/role/MFA claimを設定で差し替える。
- さくらのクラウドはUI/BFF/API/PostgreSQL/key ringの本体を保持し、IdPには本文や問いデータを渡さない。
- Keycloak等の自前IdPは、専任運用、二重化、backup、鍵rotation、緊急account、脆弱性対応が整うまで採用しない。

### 2. Entra接続境界

- tenant固有Authorityを使い、`common`、`organizations`、wildcard tenantをProductionで使用しない。
- app rolesとして`Editor`と`Reviewer`を定義し、ID tokenの`roles` claimを`RoleClaimType=roles`で検査する。
- app assignmentを受けた担当者だけにroleを付与し、role未付与userへ管理権限を与えない。
- Conditional Accessで対象enterprise applicationへMFAを要求し、report-only評価後に有効化する。
- token中のMFA証跡はtenant/policy/token versionにより差があり得る。実tokenで確認せず`amr=mfa`を仮定しない。
- 実ID tokenに`amr=mfa`がある場合は既定値を使う。Authentication Context等の別claimを採用する場合は`MfaClaimType`と`MfaClaimValue`を変更し、同じE2Eを再実行する。MFA claim検査自体は無効化しない。

### 3. OIDC protocol検証

- authorization code + PKCE S256を必須とし、試験IdP側でもcode challenge/verifierを照合する。
- confidential clientをtoken endpointで認証する。
- HTTPS discovery document、issuer、audience、lifetime、JWKS RSA署名、nonce、`sub`を検証する。
- authorization requestへ`max_age=900`を付け、BFF側も署名済み`auth_time`が16分以内（15分＋clock skew 1分）か再検査する。
- access tokenをtoken endpointから受けてもCookie ticketへ保存せず、browserへ返さない。
- IdP未登録鍵による署名、MFA証跡欠落、古い`auth_time`をsession作成前に拒否する。

### 4. Challengeとlogout

- OIDC loginを開始できる経路は`GET /bff/login`だけとする。
- `/bff/session`と管理APIの未認証要求は401を返し、暗黙にIdPへredirectしない。
- logoutはMFA sessionとCSRFを検査した`POST /bff/logout`だけで開始し、local Cookie削除とIdP end-sessionを両方行う。
- remote authentication failureは原因詳細をbrowserへ出さず、`/app/?authentication=failed`だけを返す。

## Stage 5試験IdP

test assembly内だけに、次を持つ最小OIDC providerを置く。

- 一時self-signed HTTPS certificate。証明書を試験clientでpin留めする。
- 一時RSA signing keyとJWKS endpoint。
- authorization codeの一回限り使用、PKCE S256、client secret、redirect URI、state、nonce検査。
- `sub`、`name`、Entra互換`roles`、`amr`、`auth_time`を持つ署名済みID token。
- 正常MFA、MFA欠落、未登録鍵署名、古い認証のprofile。
- end-session endpoint。

このproviderはProduction artifactへ入らない。実IdPの代替ではなく、application側OIDC実装の再現可能なprotocol試験である。

## 自動検証

- TC-043: 実HTTPSでdiscovery、authorize、code、PKCE、token、JWKS、nonce、Cookieまで往復する。
- TC-044: 正しい署名でもMFA証跡のないsessionを403にする。
- TC-045: metadata未登録鍵で署名されたID tokenを拒否する。
- TC-046: OIDC Cookie sessionの更新をlogin-bound CSRFで保護する。
- TC-047: CSRF付きlogoutでlocal CookieとIdP sessionを終了する。
- TC-048: 15分を超える古い`auth_time`を拒否する。

## 自己ループで判明した点

1. 未認証`/bff/session`が既定OIDC challengeにより自動loginしていた。既定challengeをCookie handlerへ変更し、401へ修正した。
2. `MaxAge=15分`の送信だけではASP.NET側の`auth_time`強制検査にならなかった。BFF自身の署名検証後eventで欠落・未来・期限超過を拒否した。

どちらもoption検査だけでは見つからず、実protocol往復によって発見した。

## 本番化gate

- tenant ID、client ID、redirect URI、app roles、担当者assignmentを二者確認する。
- Conditional Accessをreport-onlyで評価し、緊急accountを分離してからMFA必須を有効化する。
- 実ID tokenでissuer、audience、sub、roles、MFA claim、auth_timeを確認し、値を秘密を除いた証跡へ残す。
- client secretはsecret managerから注入し、有効期限監視とrotationを設定する。将来はclient assertion/certificateを再評価する。
- iOS/Androidの実browserでlogin、MFA、session期限、logout、account/role変更を試験する。
- 実IdP gateに合格するまでProduction管理機能を公開しない。

