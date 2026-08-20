# Stage 6R-2 Package Manifest

- Package: `16_toi-no-mori-mvs01-stage6r2-domain-v0.1.zip`
- Date: 2026-08-20
- Baseline: `14_toi-no-mori-mvs01-stage6r1-red-tests-v0.1.zip`
- Product delta: Question集約のtenant不変属性、承認対象版・承認者、差戻し/取下げ理由分離
- Test delta: 063-DOM、079-DOM、081-DOMをPython静的契約からC# native挙動試験へ移管
- Documentation delta: Stage 6R-2仕様、UML、V字追跡、赤→緑証跡
- Current result: Domain 12/12、API 32/32、OIDC 7/7、Mobile 5/6（既知TC-055 RED）
- Remaining Stage 6R contracts: 19 expected red、0 harness errors

このpackageはT2-Domainの反復成果物である。テナントの認証・認可・PostgreSQL RLS、承認APIの`If-Match`、role別DTOは未実装であり、本番候補ではない。
