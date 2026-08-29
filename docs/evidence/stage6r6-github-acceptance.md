# Stage 6R-6 GitHub Actions受入証跡

- 文書ID: QF-EVD-ST6R6-MVS01-001
- 受入日: 2026-08-21
- 判定: **ACCEPTED**

## 実行同定

- Repository: `KojimaSusumu365/toi-no-mori-mvs01`
- Workflow: `Stage 6R-6 platform security regression`
- Run number / ID: `#1` / `32435956694`
- Branch: `stage6r4c-postgresql-green-fix`
- head SHA: `419014d5cfae3f9ff438610f46b7d7330e3fa80a`
- Job: `Native platform security regression 80/80 gate`
- 実行条件: Ubuntu 24.04、非root、native、.NET SDK 10.0.400、PostgreSQL 18.6

## 受入結果

| Gate | 結果 |
|---|---:|
| CI構成 Stage 6R-4C | 6/6 |
| CI構成 Stage 6R-5 | 8/8 |
| CI構成 Stage 6R-6 | 6/6 |
| Build | 0 warnings / 0 errors |
| 試験ID一意性 | GREEN |
| Domain | 12/12 |
| API | 40/40 |
| Mobile Web | 6/6 |
| OIDC E2E | 7/7 |
| PostgreSQL実DB | 11/11 |
| DR | 4/4 |
| **native合計** | **80/80 GREEN** |

## Artifact

- Name: `stage6r6-platform-security-evidence-32435956694-1`
- Artifact ID: `9430807397`
- Size: 6480 bytes
- SHA-256: `b54439602551595837648a6a2c3e9c137e0d12ebe514a78460ec7891b990167d`
- head SHA、branch、run IDはartifact metadataと一致した。

本証跡はローカルroot環境の代替判定ではなく、GitHub非root runnerで実PostgreSQLと隔離DRをnative実行した結果である。実Entra ID、物理スマートフォン、さくら石狩・東京リージョンの災害復旧はこの受入範囲に含めない。
