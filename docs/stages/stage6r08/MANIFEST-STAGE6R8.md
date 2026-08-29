# Stage 6R-8 Mobile Approval ETag and Role DTO Package Manifest

- Specification: `docs/stages/stage6r08/stage6r8-mobile-etag-role-dto.md`
- UML: `docs/architecture/uml/uml-stage6r8-mobile-etag-role-dto.md`
- GitHub acceptance: `docs/evidence/stage6r08/stage6r8-github-acceptance.md`
- Failure-first native tests: `TC-ACC-MVS01-076-MOB`、`TC-ACC-MVS01-081-API`
- Local build: warning 0 / error 0
- Test ID uniqueness: GREEN
- Local failure-first: API 40/41、Mobile 6/7、新規2件だけ期待RED
- GitHub expected RED: Run #2 / `32796153468`、API 40/41・Mobile 6/7、他suite GREEN
- Local GREEN: Domain 12/12、API 41/41、Mobile 7/7、OIDC 7/7
- CI contract: Stage 6R-8 6/6、non-root、native exact-count 83/83、immutable evidence
- Root fail-closed: native suite未開始、exit 2、accepted=false
- Remaining failure-first contracts: 3/3 expected RED、harness error 0
- GitHub GREEN: Run #3 / `32796488019`、83/83、artifact `9545065213`
- Artifact SHA-256: `9e380a7344237b67dbdd568e5dd90804e4baa8bafa7a512d83e0cddbf533a142`
- Current status: **ACCEPTED**
