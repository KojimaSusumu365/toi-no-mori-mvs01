# Stage 6R-6 Platform Security Package Manifest

- Specification: `docs/stage6r6-platform-security-audit.md`
- UML: `docs/uml-stage6r6-platform-security.md`
- Failure-first API: existing 37/37 + new TC-070/071/080 = 37/40 RED
- Local API GREEN: 40/40
- Existing local regression: Domain 12/12、Mobile 6/6、OIDC 7/7、Build warning 0 / error 0
- PostgreSQL: migration 004、application/migration/writer/reader role split、TC-071-PGを追加。assembly build済み
- CI contract: Stage 6R-6 6/6、non-root、native exact-count 80/80、immutable evidence
- Remaining failure-first contracts: 6/6 expected RED、harness error 0
- Remote status: commit/push未実施。PostgreSQL 11/11、DR 4/4、CI 80/80は未判定
