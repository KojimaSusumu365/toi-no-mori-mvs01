# Stage 6R-8 GitHub Actions受入証跡

- 文書ID: QF-EVD-ST6R8-MVS01-001
- 受入日: 2026-08-25
- 判定: **ACCEPTED**

## 実行同定

- Repository: `KojimaSusumu365/toi-no-mori-mvs01`
- Workflow: `Stage 6R-8 mobile approval and role DTO regression`
- Run number / ID: `#3` / `32796488019`
- Branch: `stage6r4c-postgresql-green-fix`
- head SHA: `c504e3dbfe086d5d47cc5bbd69e05d03b7d5287e`
- Job ID: `97648759245`
- 実行条件: Ubuntu 24.04、非root、native、.NET SDK 10.0.400、PostgreSQL 18.6

## RED→GREEN経路

| Run | 判定 | 検出内容 |
|---|---|---|
| #1 / `32795569005` | 期待RED（観測不足） | API TC-081を検出後、旧runnerが停止してMobile以降は未実行 |
| #2 / `32796153468` | 期待RED | API TC-081とMobile TC-076だけRED、その他全suite GREEN |
| #3 / `32796488019` | GREEN | ETag/409とrole別DTO実装後、全83件合格 |

## 受入結果

| Gate | 結果 |
|---|---:|
| CI構成 Stage 6R-4C | 6/6 |
| CI構成 Stage 6R-5 | 8/8 |
| CI構成 Stage 6R-6 | 6/6 |
| CI構成 Stage 6R-7 | 6/6 |
| CI構成 Stage 6R-8 | 6/6 |
| Build | 0 warnings / 0 errors |
| 試験ID一意性 | GREEN |
| Domain | 12/12 |
| API | 41/41 |
| Mobile Web | 7/7 |
| OIDC E2E | 7/7 |
| PostgreSQL実DB | 12/12 |
| DR | 4/4 |
| **native合計** | **83/83 GREEN** |

## Artifact

- Name: `stage6r8-mobile-etag-role-dto-evidence-32796488019-1`
- Artifact ID: `9545065213`
- Size: 6588 bytes
- SHA-256: `9e380a7344237b67dbdd568e5dd90804e4baa8bafa7a512d83e0cddbf533a142`
- head SHA、branch、run IDはartifact metadataと一致した。

本証跡は審査詳細と承認ETagの束縛、409後の手動再審査、role別DTO、既存のtenant・DB・DR境界を対象とする。実Entra ID、物理スマートフォン、さくら石狩・東京リージョンの復旧訓練は受入範囲外である。Draft解除、merge、本番承認は利用者と各レビュー担当者の判断を必要とする。
