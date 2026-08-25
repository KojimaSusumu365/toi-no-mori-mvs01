# Stage 6R-9 GitHub Actions受入証跡

- 文書ID: QF-EVD-ST6R9-MVS01-001
- 受入日: 2026-08-25
- 判定: **ACCEPTED**

## 実行同定

- Repository: `KojimaSusumu365/toi-no-mori-mvs01`
- Workflow: `Stage 6R-9 OIDC tenant and self-approval regression`
- Run number / ID: `#2` / `32798692282`
- Branch: `stage6r4c-postgresql-green-fix`
- head SHA: `f2f32cfc00a60967c0ad5bae86c8bb1f3228c0bd`
- Job ID: `97655178973`
- 実行条件: Ubuntu 24.04、非root、native、.NET SDK 10.0.400、PostgreSQL 18.6

## RED→GREEN経路

| Run | 判定 | 検出内容 |
|---|---|---|
| #1 / `32798362811` | 期待RED | 既存OIDC 7件GREEN、TC-077だけ未登録組織のCookie前拒否不足でRED |
| #2 / `32798692282` | GREEN | token検証時tenant固定と予約claim除去後、全84件合格 |

## 受入結果

| Gate | 結果 |
|---|---:|
| CI構成 Stage 6R-4C | 6/6 |
| CI構成 Stage 6R-5 | 8/8 |
| CI構成 Stage 6R-6 | 6/6 |
| CI構成 Stage 6R-7 | 6/6 |
| CI構成 Stage 6R-8 | 6/6 |
| CI構成 Stage 6R-9 | 6/6 |
| Build | 0 warnings / 0 errors |
| 試験ID一意性 | GREEN |
| Domain | 12/12 |
| API | 41/41 |
| Mobile Web | 7/7 |
| OIDC E2E | 8/8 |
| PostgreSQL実DB | 12/12 |
| DR | 4/4 |
| **native合計** | **84/84 GREEN** |

## Artifact

- Name: `stage6r9-oidc-tenant-self-approval-evidence-32798692282-1`
- Artifact ID: `9545807227`
- Size: 6647 bytes
- SHA-256: `c38d09fa6c926e2d3ef7d844e8cbaf17f94ea0241b9a24276121688eea00b681`
- head SHA、branch、run IDはartifact metadataと一致した。

本証跡は独立HTTPS試験IdPを使う実protocol E2Eで、issuer付きtenant mapping、予約claim除去、未登録組織のCookie発行前拒否、dual-role自己承認拒否を対象とする。Microsoft Entra IDの実tenant、実端末、さくら石狩・東京リージョンの復旧訓練は受入範囲外である。Draft解除、merge、本番承認は利用者と各レビュー担当者の判断を必要とする。
