# Stage 6R-9 OIDC Tenant Mapping and Self-Approval Package Manifest

- Specification: `docs/stages/stage6r09/stage6r9-oidc-tenant-self-approval.md`
- UML: `docs/architecture/uml/uml-stage6r9-oidc-tenant-self-approval.md`
- GitHub acceptance: `docs/evidence/stage6r09/stage6r9-github-acceptance.md`
- Failure-first native test: `TC-ACC-MVS01-077-OIDC`
- Local build: warning 0 / error 0
- Local failure-first: OIDC 7/8、新規TC-077だけ期待RED
- GitHub expected RED: Run #1 / `32798362811`、OIDC 7/8、他suite GREEN
- Local GREEN: Domain 12/12、API 41/41、Mobile 7/7、OIDC 8/8
- CI contract: Stage 6R-9 6/6、non-root、native exact-count 84/84、immutable evidence
- Remaining failure-first contracts: 2/2 expected RED、harness error 0
- GitHub GREEN: Run #2 / `32798692282`、84/84、artifact `9545807227`
- Artifact SHA-256: `c38d09fa6c926e2d3ef7d844e8cbaf17f94ea0241b9a24276121688eea00b681`
- Current status: **ACCEPTED**
