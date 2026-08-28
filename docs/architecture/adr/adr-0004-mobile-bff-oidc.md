# ADR-0004 スマートフォンWeb UIと同一オリジンBFF/OIDC

- 状態: 採用（Stage 4。実IdP接続・実機検証待ち）
- 日付: 2026-08-16
- 対象要求: `REQ-MVS01-MOB-001`、`REQ-MVS01-MOB-002`、`REQ-MVS01-IAM-002`、`REQ-MVS01-IAM-003`、`REQ-MVS01-SEC-003`、`REQ-MVS01-SEC-004`
- 関連仕様: `QF-UML-MVS01-004`

## 背景

一般利用者はスマートフォンから公開済みの問いを検索し、編集者・ReviewerはMFAを完了した端末から管理操作を行う。ブラウザへOIDC access tokenやrefresh tokenを渡す構成は、XSSや拡張機能、誤った保存処理による流出面を広げる。Cookie認証はブラウザが自動送信するため、HTTPSだけではCSRFを防げない。

MicrosoftのASP.NET Core 10公式資料は、機密クライアントによるOIDC Authorization Code + PKCE、Webセッション用Cookie、BFFで機密認証情報をバックエンドに保持する構成を案内している。CSRF公式資料は、Cookie認証された更新要求にanti-forgery tokenが必要であり、HTTPS単独では対策にならないとしている。

参照した公式仕様:

- https://learn.microsoft.com/en-us/aspnet/core/security/authentication/configure-oidc-web-authentication?view=aspnetcore-10.0
- https://learn.microsoft.com/en-us/aspnet/core/security/anti-request-forgery?view=aspnetcore-10.0
- https://www.nuget.org/packages/Microsoft.AspNetCore.Authentication.OpenIdConnect/

## 決定

### 1. UIとBFFの境界

- ASP.NET Core API自身が `/app/` の静的UIと `/bff/*` を配信する同一オリジン構成とする。
- ブラウザは同一オリジンのHttpOnly Cookieだけを使用し、access token、refresh token、client secretを受け取らない。
- `localStorage` と `sessionStorage` へ認証情報を保存しない。外部CDN、inline script、inline styleを使わない。
- BFF session応答はsubject、表示名、role、CSRF tokenだけとし、`Cache-Control: no-store`を付ける。
- Stage 4の管理UIは下書き作成までを対象とする。更新、レビュー申請、承認、差戻し、取り下げの画面は次のUI反復で追加する。

### 2. OIDCとMFA

- Productionは`Authentication:Mode=Oidc`以外で起動しない。
- OIDCは機密クライアントのAuthorization Code flow + PKCE、HTTPS metadata、issuer検証を使用する。
- Cookieを20分、非slidingとし、`__Host-`、`Secure`、`HttpOnly`、`SameSite=Lax`、`Path=/`を強制する。
- OIDC tokenを認証ticketへ保存せず、UserInfo endpointも呼ばない。必要なsubject、name、role、`amr`は検証済みID tokenから得る。
- 管理APIとBFF session/logoutは、認証に加えて`amr=mfa`を必須とする。編集は`Editor`、審査は`Reviewer` roleをさらに要求する。
- 実IdPで`sub`、`amr=mfa`、`role=Editor|Reviewer`が発行されるようmappingし、自己承認禁止は既存Domain/API境界でも維持する。

### 3. CSRF、redirect、描画

- ログイン成功時に256-bitのCSRF tokenを暗号学的乱数で生成し、暗号化された認証ticket内のclaimへ束縛する。
- JSON更新は`X-CSRF-Token`、通常のlogout formは`csrfToken` form fieldで同じtokenを送り、固定時間比較する。
- ログイン・ログアウト後のreturn URLは`/app`配下の相対URLだけを許可する。absolute URL、protocol-relative URL、backslash、制御文字を拒否する。
- 未信頼文字列は`textContent`とDOM生成APIだけで描画し、HTML文字列挿入APIを使用しない。
- CSPはdefault denyとし、script/style/connect/font/manifestをselfへ制限する。frame、object、camera、microphone、geolocationを拒否する。

### 4. Cookie暗号鍵

- API全instanceは同じ`DataProtection:KeyRingPath`とapplication nameを使用し、ローリング更新やinstance切替後もCookieを復号できるようにする。
- key ringは共有された非公開領域へ永続化し、保存時はX.509 certificateで保護する。
- certificateはprivate keyを含むPFXとして秘密管理基盤から各APIへ注入し、repository、image、通常の設定ファイルへ置かない。
- key ring、PFX、passwordはAPI実行主体だけが読めるようにし、backup、監視、一般運用主体から分離する。

### 5. スマートフォンとアクセシビリティ

- 360 CSS px幅で1列へ折り返し、操作部品は最小44 CSS px、safe areaを考慮する。
- semantic landmark、label、skip link、visible focus、live region、reduced motion、forced colorsへ対応する。
- 公開検索はログイン不要、管理workspaceはMFA sessionがある場合だけ表示する。

## Production設定契約

値は秘密対応deployment systemから注入する。特に`ClientSecret`、DB password、PFX passwordをファイルへ保存しない。

```text
ASPNETCORE_ENVIRONMENT=Production
Persistence__Provider=PostgreSql
ConnectionStrings__PostgreSql=Host=...;Database=...;Username=...;Password=...;SSL Mode=VerifyFull;Root Certificate=...
ConnectionStrings__PostgreSqlMigrator=Host=...;Database=...;Username=...;Password=...;SSL Mode=VerifyFull;Root Certificate=...
Authentication__Mode=Oidc
Authentication__Oidc__Authority=https://identity.example
Authentication__Oidc__ClientId=toi-no-mori-production
Authentication__Oidc__ClientSecret=<secret injection>
Authentication__Oidc__NameClaimType=name
Authentication__Oidc__RoleClaimType=role
DataProtection__KeyRingPath=/run/toi-no-mori/data-protection-keys
DataProtection__CertificatePath=/run/secrets/data-protection.pfx
DataProtection__CertificatePassword=<secret injection>
```

IdPへ登録するredirect URIは外部公開originの`/signin-oidc`、post-logout URIは`/signout-callback-oidc`とする。wildcard redirect URIは使用しない。

## 自動検証

- TC-034: ProductionのOIDC未設定を起動時に拒否する。
- TC-035: code + PKCE、HTTPS metadata、token非保存、安全なCookie、共有Data Protection設定を検査する。
- TC-036: MFA証跡のない管理要求を403で拒否する。
- TC-037: BFF sessionがtokenを返さず、最小情報と`no-store`だけを返す。
- TC-038: 外部サイトへのopen redirectを拒否する。
- TC-039: 360px、44px touch target、safe area、reduced motionを静的仕様検査する。
- TC-040: label、landmark、skip link、live region、focus可視性を検査する。
- TC-041: browser token storage、危険なHTML挿入、外部resourceを使用しないことを検査する。
- TC-042: 実KestrelからUI、CSS、JavaScript、manifest、CSP、`no-store`を配信できることを検査する。

## 採用しなかった案

### ブラウザへOIDC tokenを渡すSPA

認証tokenをJavaScript実行環境へ露出し、保存・更新・失効・CORSの管理面を増やすため採用しない。将来UI frameworkを導入しても、BFF境界は維持する。

### UIを別originのCDNへ配置

CORS、CSRF、Cookie domain、CSP、障害切分けを複雑化する。MVSでは同一配信単位とし、実測負荷が必要性を示した場合にだけ再評価する。

### CookieだけでCSRF tokenを省略

SameSiteは多層防御だが、Cookie認証で全更新を保護する単独の根拠にしない。login-bound tokenを必須とする。

## 制約と本番化gate

- 実IdP、tenant、client、MFA policy、claim mapping、credentialは未接続である。Productionはこれらが揃わない限り管理機能を起動できない。
- 実ブラウザ・実スマートフォンでの画面描画、screen reader、OS text拡大、MFA往復は未検証である。
- さくらのロードバランサーでTLS終端する場合は、信頼するproxyだけから`X-Forwarded-Proto/Host`を受理する設定を追加し、生成されるOIDC redirect URIを検証する。現在のProduction既定はアプリまでHTTPSを終端する前提である。
- session失効、IdP logout、role変更反映、並行session、鍵rotationを実IdPで受入試験する。
- CSP report収集、監視通知、WAF/rate limit調整、依存関係更新を運用設計へ接続する。
