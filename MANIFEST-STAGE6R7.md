# Stage 6R-7 Append-only Database Package Manifest

- Specification: `docs/stage6r7-append-only-db.md`
- UML: `docs/uml-stage6r7-append-only-db.md`
- Failure-first native test: `TC-ACC-MVS01-073-PG`
- Local build: warning 0 / error 0
- Test ID uniqueness: GREEN
- CI contract: Stage 6R-7 6/6、non-root、native exact-count 81/81、immutable evidence
- Remaining failure-first contracts: 5/5 expected RED、harness error 0
- Failure-first remote: Run #1 (`32437227404`)、PostgreSQL 11/12、TC-073だけが期待RED
- RED artifact: ID `9431226145`、SHA-256 `bbdd80b02d456eb66b17dd79a880f1659b4f29e3dcb5f3065506d3fea99b9d4a`
- GREEN implementation: migration 005、明示REVOKE、3 mutation-prevention triggers
