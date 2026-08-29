# Stage 6R-1 失敗先行テスト実行証跡

- 最新再実行日時: 2026-08-20 12:09:25 UTC
- 対象: Stage 6 / v0.6製品コード + Stage 6R-1 test-only差分
- 実行環境: Python 3.12.13
- .NET SDK: 10.0.400（導入後に再実行）
- PostgreSQL: 18.6バイナリ導入済み、server起動はコンテナの実効UID変更禁止により未実行
- 測定範囲: `local-static-contract`
- 模擬: false（ただしbehavioral testではなく静的contract gate）

## 結果

| 検査 | 結果 |
|---|---:|
| 試験ID重複 | 0件、合格 |
| Stage 6R新規contract | 22件 |
| 期待した赤 | 22件 |
| 想定外の合格 | 0件 |
| harness error | 0件 |
| 製品acceptance合格として計上 | 0件 |

`./scripts/test-stage6r1-red.sh`は、22件がすべて`STAGE6R_IMPLEMENTATION_MISSING`で赤の時だけ`EXPECTED RED CONFIRMED`を返した。機械可読な全明細は`stage6r1-red-result.json`にある。

## 未実行

- native C#新規22件への置換
- PostgreSQL 18のmigration、RLS、pool再利用、複合FK、追記専用権限
- DRの暗号化バックアップ・隔離復元
- OIDC HTTPS往復、実browser/実スマートフォン、性能試験

.NETのrestore/compileは導入後に成功し、既存Domain/API/Mobile/OIDCは53合格・TC-055期待RED 1件まで確認した。PG/DR等を未実行のまま「85件合格」または「Stage 6R完了」と表現してはならない。
