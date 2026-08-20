# Stage 6R-3 承認API 赤→緑 実行証跡

- 実行日: 2026-08-20 UTC
- .NET SDK: 10.0.400
- Runtime: .NET / ASP.NET Core 10.0.11
- 構成: Release、警告をエラーとして扱う

## 結果

| Gate / Suite | 合格 | 失敗 | 判定 |
|---|---:|---:|---|
| 実装前API native | 32 | 1 | TC-064だけ期待RED |
| 実装後API native | 33 | 0 | GREEN |
| Domain回帰 | 12 | 0 | GREEN |
| Mobile回帰 | 5 | 1 | TC-055だけ既知RED |
| OIDC E2E回帰 | 7 | 0 | GREEN |
| Solution Release build | - | - | 警告0・エラー0 |
| 試験ID一意性 | - | - | PASSED |
| 残存Stage 6R契約 | 0 | 18 | EXPECTED RED、harness error 0 |

## RED

```text
not ok - TC-ACC-MVS01-064-API
Approval without If-Match must return 428.
Expected: PreconditionRequired; actual: OK.
result: 32 passed; 1 failed; 33 total
```

## GREENで確認した分岐

- `If-Match`欠落: 428
- weak ETag: 400
- stale ETag: 409
- stale拒否後: `IN_REVIEW`、Version 2、ETag `"2"`を維持
- current ETag: 200、`PUBLISHED`、Version 3、ETag `"3"`
- 同一key・同一版再送: 保存済みVersion 3を200で返し、二重承認なし
- 同一key・異なる版: 409

## 未実行

PostgreSQL 18.6のコードはRelease build済みだが、このWorkコンテナでは非root PostgreSQL processを開始できないためPG統合5件とDR 4件は未実行である。実IdP、物理スマートフォン、さくら石狩・東京環境も本証跡の範囲外とする。

本証跡は承認API反復の合格であり、テナント分離、Stage 6R全体、本番、災害復旧の受入承認ではない。
