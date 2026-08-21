# Stage 6R-7 GitHub Actions受入証跡

- 文書ID: QF-EVD-ST6R7-MVS01-001
- 受入日: 2026-08-21
- 判定: **ACCEPTED**

## 実行同定

- Repository: `KojimaSusumu365/toi-no-mori-mvs01`
- Workflow: `Stage 6R-7 append-only database regression`
- Run number / ID: `#3` / `32438157919`
- Branch: `stage6r4c-postgresql-green-fix`
- head SHA: `83eb08dcc93fe430a28ec13a05211c6122d0c8ce`
- Job: `Native append-only database regression 81/81 gate`
- 実行条件: Ubuntu 24.04、非root、native、.NET SDK 10.0.400、PostgreSQL 18.6

## RED→GREEN経路

| Run | 判定 | 検出内容 |
|---|---|---|
| #1 / `32437227404` | 期待RED | 既存PostgreSQL 11件GREEN、TC-073だけtrigger欠落でRED |
| #2 / `32437653848` | 回帰RED | TC-073はGREEN、TC-075がmigration件数4固定を検出 |
| #3 / `32438157919` | GREEN | migration 005を台帳期待へ含め、全gate合格 |

## 受入結果

| Gate | 結果 |
|---|---:|
| CI構成 Stage 6R-4C | 6/6 |
| CI構成 Stage 6R-5 | 8/8 |
| CI構成 Stage 6R-6 | 6/6 |
| CI構成 Stage 6R-7 | 6/6 |
| Build | 0 warnings / 0 errors |
| 試験ID一意性 | GREEN |
| Domain | 12/12 |
| API | 40/40 |
| Mobile Web | 6/6 |
| OIDC E2E | 7/7 |
| PostgreSQL実DB | 12/12 |
| DR | 4/4 |
| **native合計** | **81/81 GREEN** |

## Artifact

- Name: `stage6r7-append-only-evidence-32438157919-1`
- Artifact ID: `9431515869`
- Size: 6510 bytes
- SHA-256: `041f38a9ebfc9f42557b74a5735df8b4b25857a65fbd2e9af8d50db8766440c2`
- head SHA、branch、run IDはartifact metadataと一致した。

本証跡はローカルroot環境の代替判定ではなく、GitHub非root runnerで実PostgreSQLと隔離DRをnative実行した結果である。triggerは通常のUPDATE/DELETE誤操作を拒否するが、table ownerまたはsuperuserによるtrigger無効化やDDLまで防ぐWORM媒体ではない。実Entra ID、物理スマートフォン、さくら石狩・東京リージョンの災害復旧はこの受入範囲に含めない。
