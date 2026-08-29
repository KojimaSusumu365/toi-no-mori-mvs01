# 開発・試験環境

## 採用版

| 項目 | 版 | 配置 |
|---|---|---|
| .NET SDK | 10.0.400 | `.tools/dotnet` |
| ASP.NET Core runtime | 10.0.11 | `.tools/dotnet/shared` |
| PostgreSQL | 18.6 | `.tools/postgresql` |
| GNU M4 | 1.4.20 | `.tools/build-tools` |
| GNU Bison | 3.8.2 | `.tools/build-tools` |
| Flex | 2.6.4 | `.tools/build-tools` |

PostgreSQL 18.6は18系の最新セキュリティ修正版として採用した。18.4固定ではなくmajor 18＋最新minorを原則とする。

## 導入

Ubuntu 24.04 x86_64で次を実行する。

```bash
./scripts/install-local-toolchain.sh
```

公式HTTPS配布物を取得し、固定ハッシュを検証してからプロジェクト内へ導入する。システムの`/usr`や`/opt`を書き換えない。`.tools`、`.nuget-packages`、`.dotnet-cli-home`はGit管理対象外である。

## 確認

```bash
./scripts/verify-toolchain.sh
./scripts/build.sh
./scripts/test.sh
./scripts/test-postgresql.sh
./scripts/test-disaster-recovery.sh
```

rootで実行する場合、PostgreSQL試験スクリプトは通常`nobody`へ実効ユーザーを切り替える。コンテナが`setuid/setgroups`を禁止している場合、PostgreSQL本体をroot対応へ改造せず、非root runnerを持つCIまたは開発コンテナでPG/DR試験を実行する。

## セキュリティ上の注意

- .NETとPostgreSQLはパッチ版を固定し、更新時にハッシュと本書を改訂する。
- PostgreSQLのroot実行制限を解除しない。
- ローカル試験のtrust認証・TLS無効接続を本番へ流用しない。
- Productionは既存契約どおりPostgreSQL TLS `VerifyFull`、role分離、秘密注入を必須とする。
