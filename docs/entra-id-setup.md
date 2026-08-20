# Microsoft Entra ID 接続準備書

対象は「問いの森 CORE」のEditor/Reviewerだけである。一般公開検索の利用者にはloginを要求しない。これはportal操作の実施記録ではなく、実tenantが決まった後に二者確認で実施する準備手順である。

## 1. 事前決定

- 単一tenantか。初期構成はsingle tenantを推奨する。
- tenant管理責任者とapplication運用責任者は誰か。
- Conditional Accessを利用できるlicenseか。
- 通常MFAを失った場合のrecovery手順と緊急accountを誰が管理するか。
- Production外部originは何か。例: `https://core.example.jp`

公式資料:

- OIDC: https://learn.microsoft.com/en-us/entra/identity-platform/v2-protocols-oidc
- app roles: https://learn.microsoft.com/en-us/entra/identity-platform/howto-add-app-roles-in-apps
- MFA Conditional Access: https://learn.microsoft.com/en-us/entra/identity/conditional-access/policy-all-users-mfa-strength
- claim validation: https://learn.microsoft.com/en-us/entra/identity-platform/claims-validation

## 2. App registration

1. Entra admin centerで新しいapp registrationを作る。
2. Supported account typesは、この組織directoryだけを選ぶ。
3. PlatformはWebを選ぶ。SPAとして登録しない。
4. redirect URIを完全一致で登録する。
   - `https://<production-origin>/signin-oidc`
   - test/stagingは別app registrationまたは明示した別URIとする。
5. front-channel logout URLまたはpost-logout URIを製品仕様に合わせて登録する。
   - `https://<production-origin>/signout-callback-oidc`
6. wildcard、HTTP Production URI、`localhost` Production URIを登録しない。
7. tenant ID、application/client ID、object IDを設定記録へ残す。client secretは同じ記録へ書かない。

## 3. Confidential client credential

1. 初期接続では期限付きclient secretを作る。
2. 値を表示時にsecret managerへ直接登録し、repository、Wiki、chat、shell historyへ貼らない。
3. 有効期限の30日前までに通知する。
4. 新旧credentialを一時併存させてrotationし、切替後に旧credentialを無効化する。
5. 運用確立後、client certificate/private-key JWTへの移行をADRで評価する。

## 4. App roles

次のuser/group向けapp roleを作る。

| Display name | Value | 用途 |
|---|---|---|
| Question Editor | `Editor` | 下書き作成・更新・レビュー申請 |
| Question Reviewer | `Reviewer` | 差戻し・承認・取り下げ |
| Tenant Auditor | `Auditor` | 同一tenantの許可リスト型監査metadata閲覧 |

Enterprise applicationsのUsers and groupsから担当者または管理したsecurity groupへ割り当てる。Microsoft公式仕様では、user sign-in時のapp roleはID tokenの`roles` claimに入る。アプリ設定は`RoleClaimType=roles`とする。

## 5. MFA policy

1. 対象enterprise applicationを選ぶConditional Access policyを作る。
2. 対象はEditor/Reviewer候補userまたはgroupとする。
3. Grant controlでMFAまたは承認済みauthentication strengthを必須にする。
4. 緊急accountは日常利用せず、別の強固な保管・監視を行う。
5. 最初はreport-onlyでsign-in logと影響を確認する。
6. test userの成功・失敗・recoveryを確認してからOnにする。

MFA policyを有効にしただけでapplication側claim検査を削除してはならない。実際のID tokenでMFA証跡claimを確認する。`amr=mfa`が存在する場合は既定設定を使う。異なるclaimを使う場合は、意味をtenant管理者とSecurity Reviewerが確認してから`MfaClaimType`と`MfaClaimValue`を変更する。

## 6. Production設定

非秘密値の例:

```text
Authentication__Mode=Oidc
Authentication__Oidc__Authority=https://login.microsoftonline.com/<tenant-id>/v2.0
Authentication__Oidc__ClientId=<application-client-id>
Authentication__Oidc__NameClaimType=name
Authentication__Oidc__RoleClaimType=roles
Authentication__Oidc__MfaClaimType=amr
Authentication__Oidc__MfaClaimValue=mfa
Authentication__Oidc__MaxAuthenticationAgeMinutes=15
Tenancy__Organizations__Mvs01__Issuer=<verified-token-issuer>
Tenancy__Organizations__Mvs01__ExternalOrganizationId=<verified-external-organization-id>
Tenancy__Organizations__Mvs01__InternalTenantId=<internal-tenant-uuid>
```

管理APIは検証済みprincipalの`iss`と`external_organization_id`の組を読み、上記許可表で内部UUIDへ変換する。Entraの標準`tid`等を使用する場合は、claims mappingで意味を確認した値を`external_organization_id`へ正規化する設計と受入を先に完了する。token中の値をDBのtenant UUIDとして直接使用しない。issuer不一致、claim欠落、複数、未登録は403で停止する。外部組織IDと内部UUIDをBFF session JSONへ返さない。

secret managerから注入する値:

```text
Authentication__Oidc__ClientSecret
```

実ID tokenに`amr=mfa`がない場合、この例をそのまま本番化しない。MFA証跡の設計を確定するまでアプリは403で安全側に停止する。

## 7. 接続受入

| ID | 確認 |
|---|---|
| ENTRA-AT-01 | 未割当userは管理workspaceへ入れない |
| ENTRA-AT-02 | Editorは下書きを作れるが承認できない |
| ENTRA-AT-03 | Reviewerは承認できるが自己承認できない |
| ENTRA-AT-04 | MFA未完了またはMFA証跡欠落を拒否する |
| ENTRA-AT-05 | issuer/audience/signature/nonce不正を拒否する |
| ENTRA-AT-06 | 15分を超える認証で再loginが必要になる |
| ENTRA-AT-07 | logout後のBFF sessionを401にする |
| ENTRA-AT-08 | role削除・account無効化後の反映時間を測る |
| ENTRA-AT-09 | client secret rotation中も一方のcredentialで継続できる |
| ENTRA-AT-10 | iOS Safari、Android Chrome、desktop browserでMFA往復する |
| ENTRA-AT-11 | 組織claim欠落・未登録を拒否し、登録済み組織だけを対応する内部tenantへ限定する |

## 8. 証跡に残さないもの

- client secret値
- ID token、access token、refresh tokenの全文
- Cookie値、nonce、authorization code、PKCE verifier
- MFA電話番号、回復code、個人端末情報

証跡にはUTC時刻、test ID、tenant/clientの秘密でない識別子、期待結果、合否、correlation IDだけを残す。
