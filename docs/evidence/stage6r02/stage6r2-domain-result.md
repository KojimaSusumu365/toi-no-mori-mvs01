# Stage 6R-2 Domain層 赤→緑 実行証跡

- 実行日: 2026-08-20 UTC
- .NET SDK: 10.0.400
- Runtime: .NET / ASP.NET Core 10.0.11
- 構成: Release、警告をエラーとして扱う

## 結果

| Gate / Suite | 合格 | 失敗 | 判定 |
|---|---:|---:|---|
| 実装前Domain build | 0 | 23 compile errors | 期待RED確認 |
| 実装後Domain native | 12 | 0 | GREEN |
| Solution Release build | - | - | 警告0・エラー0 |
| API回帰 | 32 | 0 | GREEN |
| Mobile回帰 | 5 | 1 | TC-055だけが承認済み仕様に対する既知RED |
| OIDC E2E回帰 | 7 | 0 | GREEN |
| 試験ID一意性 | - | - | PASSED |
| 残存Stage 6R契約 | 0 | 19 | EXPECTED RED、harness error 0 |

## Domain native 12件

```text
ok TC-ACC-MVS01-003
ok TC-ACC-MVS01-006
ok TC-ACC-MVS01-007
ok TC-ACC-MVS01-008
ok TC-ACC-MVS01-009
ok TC-ACC-MVS01-010
ok TC-ACC-MVS01-012
ok TC-ACC-MVS01-013
ok TC-ACC-MVS01-016
ok TC-ACC-MVS01-063-DOM
ok TC-ACC-MVS01-079-DOM
ok TC-ACC-MVS01-081-DOM
result: 12 passed; 0 failed; 12 total
```

TC-079-DOMは固定seed `20260820`で500系列×20操作を実行する。成功commandの版+1、tenant不変、公開承認情報、拒否commandのSnapshot完全不変を検査した。

## 実装前REDの代表診断

```text
CS1501: No overload for method 'Approve' takes 3 arguments
CS1061: 'Question' does not contain a definition for 'ApprovedVersion'
CS1061: 'Question' does not contain a definition for 'ApprovedBy'
CS1061: 'Question' does not contain a definition for 'TenantId'
CS1061: 'Question' does not contain a definition for 'WithdrawalReason'
CS1729: 'Question' does not contain a constructor that takes 7 arguments
Build FAILED. 0 Warning(s), 23 Error(s)
```

## 未実行

PostgreSQL server、PG統合5件、DR 4件はこのWorkコンテナの実効UID変更禁止により未実行である。導入済みPostgreSQL 18.6のバイナリ確認と、実DB試験の合格は同一視しない。実IdP、物理スマートフォン、さくら石狩・東京の実クラウドも本証跡の範囲外である。

本証跡はT2-Domainだけの合格証跡であり、Stage 6R全体、本番、災害復旧、テナント分離の受入承認ではない。
