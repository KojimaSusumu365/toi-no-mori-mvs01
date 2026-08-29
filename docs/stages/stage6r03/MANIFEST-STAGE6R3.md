# Stage 6R-3 Package Manifest

- Package: `17_toi-no-mori-mvs01-stage6r3-approval-api-v0.1.zip`
- Date: 2026-08-20
- Baseline: `16_toi-no-mori-mvs01-stage6r2-domain-v0.1.zip`
- Product delta: 承認APIのstrong `If-Match`必須化、版付きStore契約、承認後ETag
- Client delta: Mobile Web、OIDC E2E、API/PG test clientが承認対象版を送信
- Idempotency delta: 問い・Reviewer・承認対象版を指紋へ含める
- Test delta: TC-064-APIをPython静的契約からC# native挙動試験へ移管
- Documentation delta: Stage 6R-3仕様、UML、V字追跡、赤→緑証跡
- Current result: API 33/33、Domain 12/12、OIDC 7/7、Mobile 5/6（既知TC-055 RED）
- Remaining Stage 6R contracts: 18 expected red、0 harness errors

このpackageはT2承認APIの反復成果物である。テナントの認証・認可・PostgreSQL RLS、監査境界、role別DTO、Mobileの競合再審査導線は未実装であり、本番候補ではない。
