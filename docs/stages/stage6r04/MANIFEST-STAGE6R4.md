# Stage 6R-4 Package Manifest（Package 18履歴）

> この文書はPackage 18作成時点の記録である。現行差分と判定は`docs/stages/stage6r04/MANIFEST-STAGE6R4-DB.md`を正とする。

- Package: `18_toi-no-mori-mvs01-stage6r4-tenant-boundary-v0.1.zip`
- Date: 2026-08-20
- Baseline: `17_toi-no-mori-mvs01-stage6r3-approval-api-v0.1.zip`
- API delta: 外部組織claim許可表、内部tenant context、欠落/未登録403、同一Problem Details 404
- Store delta: 全管理操作・監査・冪等scopeへtenant UUIDを必須伝搬
- PostgreSQL delta: Migration 002/003、tenant列、revision、RLS ENABLE/FORCE、複合FK、transaction-local tenant
- Test delta: TC-065/069をnative APIへ、TC-066/067/068/074/075をnative PostgreSQLへ移管
- Documentation delta: Stage 6R-4仕様、UML、V字追跡、赤→緑、PostgreSQL未実行証跡
- Current result: Build PASS、Domain 12/12、API 35/35、OIDC 7/7、Mobile 5/6（既知TC-055 RED）
- PostgreSQL result: 10件build済み、実行0件、Stage 6R-4新規5件は未合格
- PostgreSQL acceptance blocker: migration用DBロールとapplication用DBロールの分離、およびapplicationロールが非owner・非superuser・非BYPASSRLSであることの起動時診断は未実装。TC-066-PGはこれを検出するため、実DBを起動できても現状の単一接続設定では合格にしない
- Remaining Stage 6R contracts: 11 expected red、0 harness errors

このpackageはtenant縦切りの作業成果物である。実PostgreSQL、platform監査、Auditor API、実Entra、実端末、DR、さくら実環境のgateが未完了であり、本番候補ではない。
