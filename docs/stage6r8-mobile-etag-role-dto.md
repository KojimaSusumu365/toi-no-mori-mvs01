# Stage 6R-8 スマートフォン承認ETag・role別DTO RED→GREEN仕様書

- 文書ID: QF-ST6R8-MVS01-001
- 版: Version 0.1
- 日付: 2026-08-25
- 入力基準: 承認済みADR-0008 D1/D4、ADR-0009 D7
- 対象試験: `TC-ACC-MVS01-076-MOB`、`TC-ACC-MVS01-081-API`
- 現在判定: **ローカル失敗先行RED確認済み**

## 1. 目的

Reviewerがスマートフォンで実際に審査した問いのETagだけを承認要求へ使用し、競合時に新版を自動承認しない。あわせて、Editor、Reviewer、Publicへ返すDTOを分離し、取下げ理由などrole外の情報を応答へ混在させない。

## 2. V字の仕様・試験対

| 左側仕様 | 失敗先行試験 | GREEN実装 | 受入条件 |
|---|---|---|---|
| 審査詳細と承認版を束縛 | Mobile TC-076 | 詳細GETのETagを`approvalEtag`へ保持 | ETagなしでは承認不可、承認は保持ETagだけを送信 |
| 409後に人の再審査を要求 | Mobile TC-076 | 競合専用表示と承認ボタン無効化 | 自動再送せず、明示的な再読込・再審査を要求 |
| Editor理由可視性 | API TC-081 | `EditorQuestionResponse` | `reviewReason`のみ許可し、`withdrawalReason`と`ownerSubject`を返さない |
| Reviewer理由可視性 | API TC-081 | `ReviewerQuestionResponse` | owner、reviewReason、withdrawalReasonを許可リストで返す |
| Public最小DTO | API TC-081 | `PublicQuestionResponse`維持 | owner、内部version、両理由を返さない |
| 全体回帰を正確件数で閉じる | Stage 6R-8 CI契約 | 非root native wrapper | API 41、Mobile 7、全83件 |

## 3. スマートフォン承認契約

1. Reviewer一覧を取得する。
2. `IN_REVIEW`の各候補について管理詳細を取得する。
3. 詳細応答のstrong ETagを、その詳細と一体の`approvalEtag`として保持する。
4. ETagが空なら承認ボタンを無効化し、再読込を案内する。
5. 承認時は`If-Match: <approvalEtag>`と新しい`Idempotency-Key`を送る。
6. 409ではETagを破棄し、自動再送・自動再取得を行わず、「一覧を再読込」後の再審査を要求する。

一覧の`version`からETagを推測しない。競合後に新版を自動取得して承認を再送すると、Reviewerが読んでいない内容を公開できるため禁止する。

## 4. role別DTO契約

| 項目 | Editor | Reviewer | Public |
|---|---:|---:|---:|
| title/body/tags/status | 可 | 可 | 公開中のみ可 |
| version | 可 | 可 | 不可 |
| ownerSubject | 不可 | 可 | 不可 |
| reviewReason | 可 | 可 | 不可 |
| withdrawalReason | 不可 | 可 | 不可 |
| createdAt/updatedAt/publishedAt | 可 | 可 | publishedAtのみ |

DTOは許可リスト型として定義し、同じ汎用管理DTOをrole間で共有しない。認可とDTO分離は別の境界であり、認可済みでも不要項目は返さない。

## 5. 受入gate

| Suite | 必須件数 |
|---|---:|
| Domain | 12 |
| API | 41 |
| Mobile | 7 |
| OIDC E2E | 7 |
| PostgreSQL | 12 |
| DR | 4 |
| **合計** | **83** |

Build警告0・エラー0、試験ID一意、残存失敗先行契約3/3 expected RED、非root native exact-count 83/83、immutable artifactを必須とする。

## 6. ローカル失敗先行証跡

- Build: warning 0 / error 0
- 試験ID一意性: GREEN
- API: 既存40件GREEN、`TC-ACC-MVS01-081-API`だけRED（Editor応答に`ownerSubject`が混在）
- Mobile: 既存6件GREEN、`TC-ACC-MVS01-076-MOB`だけRED（詳細ETag保持なし）
- 残存failure-first registry: 3/3 expected RED、harness error 0
- CI構成契約: Stage 6R-4C 6/6、6R-5 8/8、6R-6 6/6、6R-7 6/6、6R-8 6/6
- root fail-closed: native suite未開始、exit 2、accepted=false

このREDは実装前欠落を示す証拠であり、Stage 6R-8の受入合格ではない。
