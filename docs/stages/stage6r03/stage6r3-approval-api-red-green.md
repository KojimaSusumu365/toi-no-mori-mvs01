# Stage 6R-3 承認API 赤→緑 仕様・実施記録

- 文書ID: QF-ST6R3-MVS01-001
- 版: Version 0.1
- 日付: 2026-08-20
- 入力基準: 承認済みADR-0008 D1、Stage 6R-2 Domain版付き承認
- 判定: **TC-ACC-MVS01-064-APIをnative挙動試験へ移管し、赤→緑を完了**

## 1. 反復の境界

本反復は`POST /api/admin/questions/{id}/approve`の楽観的排他制御だけを完結させる。外部組織IDのtenant変換、PostgreSQL RLS、監査API再設計、role別DTO、Mobileの409再審査導線は残存契約として次反復以降へ残す。

## 2. HTTP契約

| 条件 | 応答 | 永続状態 |
|---|---|---|
| 未認証・role/MFA/CSRF不備 | 既存authorization/filter契約の401/403 | 無変更 |
| `If-Match`欠落 | 428 Precondition Required | Storeを呼ばず無変更 |
| 弱いETag、複数値、`*`、非整数、非正数 | 400 Validation Problem | Storeを呼ばず無変更 |
| `Idempotency-Key`欠落または128文字超 | 400 Validation Problem | Storeを呼ばず無変更 |
| `If-Match`が現在版と不一致 | 409 Conflict | Domain拒否、集約は無変更、拒否監査 |
| 現在版・別Reviewer・有効な冪等キー | 200 OK | `PUBLISHED`、版を1増加、成功監査、冪等応答保存 |
| 同一キー・同一問い・同一Reviewer・同一承認対象版の再送 | 200 OK | 元の応答を返し、再承認・成功監査追加なし |
| 同一キーを異なる承認対象版へ流用 | 409 Conflict | 無変更 |

`If-Match`は単一のstrong ETagかつ`"N"`形式の正整数だけを受理する。成功応答の`ETag`には承認後の新しい集約版を設定する。

## 3. 実装境界

### 3.1 Endpoint

`ApproveQuestion`はhandler到達後、`If-Match`、`Idempotency-Key`の順に検証する。EndpointのauthorizationとCSRF filterはhandlerより先に働く。解析した`expectedVersion`を`IQuestionStore.ApproveAsync`へ明示的に渡す。

### 3.2 StoreとDomain

InMemory/PostgreSQLの両Storeは次の同じ契約を持つ。

```text
ApproveAsync(id, reviewer, expectedVersion, idempotencyKey, correlationId)
```

冪等指紋は`questionId + reviewer + expectedVersion`を含む。新規commandだけがDomainの`Approve(reviewer, expectedVersion, now)`を呼び、同一指紋の再送は保存済みSnapshotを返す。

### 3.3 クライアント

- スマートフォンWeb UIはレビュー一覧に表示した`question.version`をstrong `If-Match`として送信する。
- OIDC E2EはReviewer queueから得た版を送信する。
- API/PostgreSQL試験helperも承認対象版を必須引数とする。

## 4. 赤→緑

1. TC-064をPython静的契約からC# API suiteへ移した。
2. 製品変更前は既存32件合格、新規TC-064だけが「`If-Match`欠落でも200」となり赤であることを確認した。
3. Endpoint、Store、ブラウザ、OIDC、試験clientを版付き契約へ変更した。
4. API 33件すべてを緑にした。
5. Domain、Mobile、OIDC、全solution build、ID一意性、残存18契約を回帰確認した。

## 5. 移行上の注意

旧clientは`If-Match`を送らないため、更新後は428で安全側に停止する。Web UIと同梱試験clientは更新済みだが、外部clientが存在する場合は契約変更を事前通知する。

既存PostgreSQLに保存済みの旧形式冪等指紋は承認対象版を含まない。稼働中環境へ導入する前に、旧承認要求の再送期間を閉じるcutover手順、対象行の保持・失効方針、Stage 6R migrationとの整合を定める。形式不一致時に旧結果を推測して返さず409で停止する。

## 6. 再実行

```bash
./scripts/test-stage6r3-approval-api.sh
```

このgateは試験ID一意性、API 33件GREEN、残存18件の期待REDを検査する。残存REDを合格数へ含めない。

## 7. Gate判定

- T2-Domain: 3件GREEN。
- T2-Approval API: TC-064-API GREEN。
- T2残存: 18件期待RED。
- PostgreSQL/DR実process: 実行環境制約により未実行。
- T3 全85件: 未実施。
- T4 外部受入: 未実施。

次はStage 6R-4として、tenant resolverだけを単独で「安全」と見なさず、外部組織claim、内部TenantId、可視性404、PostgreSQL tenant列・RLSを一続きの縦切りで設計する。
