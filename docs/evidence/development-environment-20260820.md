# 開発環境導入・検証証跡

- 日付: 2026-08-20
- OS: Ubuntu 24.04.3 LTS / Linux x86_64
- 配置方式: project-local

## 導入結果

| 項目 | 結果 |
|---|---|
| .NET SDK 10.0.400 | 導入・実行確認済み |
| .NET / ASP.NET Core runtime 10.0.11 | 導入・実行確認済み |
| PostgreSQL 18.6 client/server tools | build・link・version確認済み |
| PostgreSQL OpenSSL | `--with-ssl=openssl`でbuild |
| NuGet restore | 成功 |
| Release build | 成功、警告0、エラー0 |

## .NET試験

| Suite | 合格 | 失敗 | 備考 |
|---|---:|---:|---|
| Domain | 9 | 0 | 実行済み |
| API | 32 | 0 | 実Kestrel |
| Mobile | 5 | 1 | TC-055が承認済みAuditor仕様に対して期待RED |
| OIDC E2E | 7 | 0 | 実HTTPS試験IdP |
| **合計** | **53** | **1** | 失敗はStage 6Rの想定差分 |

## PostgreSQL/DRの実行状態

PostgreSQLバイナリと共有libraryは正常である。しかし、このWorkコンテナは`runuser`の`setgroups`と`setpriv`の`setresuid`を禁止している。PostgreSQLはroot起動を安全機構として拒否するため、サーバー起動・PG 5件・DR 4件は未実行とした。

root拒否を外す改造や実効UID偽装は行わない。非root runnerを持つCIまたは通常のLinux環境で再実行する。

## 供給元ハッシュ

- .NET SDK archive SHA-512: `1033977dd837150e0814cf0c5d5b17ceb63925fda7ba2158b47258a4bd7c048cf82eac3bc1166f3146f53124a3f5fba09db1de1260d2ce96399860303b404b48`
- PostgreSQL 18.6 source SHA-256: `555610c24d53e4316da5b7d3fc25c279d96856d5e0e23ee308c328c5fa881d9f`

未実行の9件を合格数へ含めない。
