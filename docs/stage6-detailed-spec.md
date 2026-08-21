# QF-MVS01-S6-001 Stage 6 最小業務フロー詳細仕様書

- Version: 0.8
- 日付: 2026-08-16
- 対象: 問いの森 CORE / MVS-01
- 方式: V字工程を基本とするアジャイル反復

## 1. 目的

スマートフォンの同一オリジンWeb UIから、問いの作成、編集、レビュー申請、差し戻し、再編集、承認、公開、取り下げ、監査確認までを完結させる。仕様変更と同じ反復で受入テストを追加し、既存の認証、公開、永続化、災害復旧境界を変更しない。

## 2. 利用者と権限

| 利用者 | 認証条件 | 可能な操作 | 禁止事項 |
|---|---|---|---|
| 一般利用者 | 不要 | 公開済みの問いを検索・閲覧 | 下書き・内部属性の閲覧 |
| Editor | OIDC、MFA証跡、Editor role | 自分の問いの作成・一覧・詳細・編集・申請 | 他Editorの問いの閲覧・編集、承認 |
| Reviewer | OIDC、MFA証跡、Reviewer role | 全管理対象の一覧・詳細、差し戻し、承認、取り下げ | 自分が作成した問いの承認、監査閲覧 |
| Auditor | OIDC、MFA証跡、Auditor role | 同一tenantの許可リスト型監査metadataを上限付きで閲覧 | 問いの編集・審査、platform監査の閲覧 |
| PlatformAuditor | OIDC、MFA証跡、PlatformAuditor role | 期間必須のplatform拒否metadata閲覧 | tenant監査、問いの編集・審査、無期限・無上限照会 |

EditorとReviewerを兼務する利用者にも自己承認禁止を適用する。画面上の無効化は補助であり、最終判定はDomain/APIで行う。

## 3. 機能要求

### REQ-MVS01-UI-001 編集ワークスペース

- Editorには本人所有の問いだけを更新日時降順で表示する。
- `DRAFT`は編集とレビュー申請を可能にする。
- 編集は画面に表示した`version`を`If-Match`として送信する。
- `409 Conflict`時は自動上書きせず、再読込を案内する。
- `ReviewReason`がある場合は再作業理由として表示し、正常な更新後に消去する。

### REQ-MVS01-UI-002 審査ワークスペース

- Reviewerには`IN_REVIEW`のレビュー待ちと`PUBLISHED`の公開中一覧を表示する。
- 差し戻しと取り下げは空でない理由を必須とする。
- 承認にはブラウザ暗号乱数による`Idempotency-Key`を付与する。
- 自己承認対象は画面で無効化し、APIでも403にする。
- 未定義の一覧状態フィルターは400にする。

### REQ-MVS01-UI-003 業務フロー完結

異なるsubjectを持つEditorとReviewerにより、次を一連の受入フローとして成立させる。

1. Editorが下書きを作成・編集する。
2. Editorがレビューを申請する。
3. Reviewerがレビュー待ち一覧から対象を確認する。
4. Reviewerが承認する。
5. 未認証の一般利用者が公開APIから取得する。

### REQ-MVS01-UI-004 差し戻し再作業

Reviewerが入力した理由をEditorへ表示し、Editorが修正して再申請できること。差し戻し後の状態は`DRAFT`、版は増加し、修正保存後は処理済み理由を消去する。

### REQ-MVS01-AUD-001 tenant監査ワークスペース

- 監査タブと`GET /api/ops/audit`はAuditor roleだけへ提供する。
- Reviewer roleだけでは監査APIを403で拒否する。
- `limit`は省略時50、許容範囲1〜200とし、範囲外は400で拒否する。
- 問い別経路は同一tenantの指定対象だけを返し、他tenantの存在を開示しない。
- 応答はactor、target ID、action、result、correlation ID、発生時刻の許可リストDTOとし、本文・token・秘密値・内部tenant IDを返さない。

### REQ-MVS01-SEC-006 管理閲覧境界

- `GET /api/admin/questions`と`GET /api/admin/questions/{id}`はOIDC認証、MFA証跡、EditorまたはReviewer roleを必須とする。
- Editorの管理閲覧はDB問い合わせ段階で`owner_subject = sub`へ限定する。
- 他EditorのIDを指定した詳細取得は情報列挙を避けるため404とする。
- Reviewerは審査に必要な管理属性を取得できる。
- 管理更新は従来どおりCSRF、role、所有者、状態、版、冪等性を検査する。
- access token、ID token、client secretをDOM、Web Storage、BFF session JSONへ出力しない。

### REQ-MVS01-AUD-002 platform拒否監査

- tenantを確定できない拒否をtenant監査へ混在させず、`platform_security_events`へ記録する。
- 生IPは保存せず、rotation可能な秘密付きHMAC partitionへ変換する。
- 429は同一partition・正規化action・UTC 1分窓で先頭だけを書込み、以後は抑制metricへ集約する。
- PlatformAuditor APIは`from`、`to`を必須とし、最大31日・最大200件に制限する。
- tenant AuditorとPlatformAuditorを分離し、相互の監査APIを403で拒否する。

## 4. 画面仕様

| 画面/領域 | 主な表示 | 主な操作 | 状態通知 |
|---|---|---|---|
| 公開検索 | キーワード、タグ、公開カード | 検索、条件消去 | 件数、取得失敗 |
| 編集 | 入力フォーム、自分の問い、版、差戻し理由 | 新規、編集、申請、再読込 | 保存、競合、権限・session失敗 |
| 審査 | レビュー待ち、公開中、作成者 | 差戻し、承認、取り下げ | 処理中、完了、競合・権限失敗 |
| 監査 | 直近50件の操作、結果、actor、対象、追跡ID | 再読込 | 件数、取得失敗 |

幅360pxから1列で操作できること、操作部品の最小高さを44 CSS pxとすること、label、landmark、visible focus、live region、safe area、reduced motionを維持する。

## 5. HTTP契約追加

### GET /api/admin/questions

Query:

- `status`: 省略、`DRAFT`、`IN_REVIEW`、`PUBLISHED`、`WITHDRAWN`
- `limit`: 省略時50、1〜100へ丸める

応答は`QuestionResponse[]`。Editorには本人所有だけ、Reviewerには全対象を返す。本文を含むため`Cache-Control: no-store`対象とする。

### GET /api/admin/questions/{id}

応答は`QuestionResponse`と現在版の`ETag`。権限内に存在しなければ404。未認証401、MFAまたはrole不足403。

### GET /api/ops/audit / GET /api/ops/audit/questions/{id}

Auditor、MFA、内部tenant解決を必須とする。`limit`は省略時50、1〜200。Reviewerだけの要求は403、旧`/api/admin/audit`は404とする。応答は`AuditRecordResponse[]`で、`Cache-Control: no-store`対象とする。

### GET /api/platform/security-events

PlatformAuditorとMFAを必須とする。`from`/`to`はUTC round-trip形式、最大31日、`limit`は1〜200。応答からpartition hash、tenant、subject、生IP、本文、claim、token、Cookieを除外し、`Cache-Control: no-store`対象とする。

## 6. セキュリティ設計

- 認証はOIDC Authorization Code + PKCE、BFF Cookie方式を継続する。
- CookieはSecure、HttpOnly、SameSite=Lax、host-only、20分、非slidingとする。
- 管理更新はlogin-bound CSRF tokenをヘッダーまたはlogout formで照合する。
- DOM生成は`textContent`を使用し、HTML文字列挿入APIを使用しない。
- 外部CDN、inline script/style、Web Storageを使用しない。
- CSP、frame拒否、MIME sniffing拒否、referrer抑止、不要device permission拒否を継続する。
- 監査画面に本文、credential、tokenを表示しない。

## 7. 仕様と受入テスト

| 要求ID | 受入テスト | 合格条件 |
|---|---|---|
| UI-001 | TC-049、055 | Editor一覧が本人所有だけで、編集画面構造を持つ |
| UI-002 | TC-050、054、055 | Reviewer queue、状態絞込、審査画面構造が成立 |
| SEC-006 | TC-051、056、058、059 | 詳細の所有者境界、ETag、CSRF、If-Match、冪等キー、PostgreSQL行scope、理由長上限 |
| UI-003 | TC-052、057 | API受入と異なる実OIDC sessionの両方で公開まで完結 |
| UI-004 | TC-053 | 差戻し理由を保持し、修正保存で消去 |
| AUD-002 | TC-070-API、071-API/PG、080-API | ID分離、429抑制、PlatformAuditor/API/DB role分離、sink障害時の元応答維持 |

## 8. 完了条件と保留gate

ローカルStage 6完了条件:

- Release buildが警告0、エラー0。
- Domain、API、Mobile、OIDC E2Eの54件が全合格。
- JavaScript構文検査が合格。
- 要求、UML、実装、テストIDの追跡が維持される。

本番開始には含めない事項:

- 実Entra tenant、Conditional Access、実MFA claimの受入
- Chromium/WebKit、iOS/Android実機、screen readerによる画面操作
- さくらのクラウド実環境、Load Balancer proxy trust、CRR、GSLB、東京復旧
- 今回の実行環境で再実行できなかったPostgreSQL 5件/DR 4件

これらはローカル実装完了とは別の受入gateとする。
