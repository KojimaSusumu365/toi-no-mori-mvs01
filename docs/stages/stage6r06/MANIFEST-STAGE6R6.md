# Stage 6R-6 Platform Security Package Manifest

- Specification: `docs/stages/stage6r06/stage6r6-platform-security-audit.md`
- UML: `docs/architecture/uml/uml-stage6r6-platform-security.md`
- Failure-first API: existing 37/37 + new TC-070/071/080 = 37/40 RED
- Local API GREEN: 40/40
- Existing local regression: Domain 12/12、Mobile 6/6、OIDC 7/7、Build warning 0 / error 0
- PostgreSQL: migration 004、application/migration/writer/reader role split、TC-071-PGを追加。assembly build済み
- CI contract: Stage 6R-6 6/6、non-root、native exact-count 80/80、immutable evidence
- Remaining failure-first contracts: 6/6 expected RED、harness error 0
- Remote acceptance: Run #1 (`32435956694`)、head `419014d5cfae3f9ff438610f46b7d7330e3fa80a`、PostgreSQL 11/11、DR 4/4、全80/80 GREEN
- Evidence: `docs/evidence/stage6r06/stage6r6-github-acceptance.md`、artifact ID `9430807397`、SHA-256 `b54439602551595837648a6a2c3e9c137e0d12ebe514a78460ec7891b990167d`
