# MANIFEST — Stage 6R-11 Question Forest Minimum / Town Readiness

## Scope

Stage 6R-11 validates that the existing Question Forest Minimum can remain the upstream CORE when Virtual Town is later attached as a separate Domain.

No Virtual Town runtime is added in this Stage.

## Specification / test pair

- Specification: `docs/stages/stage6r11/stage6r11-town-readiness.md`
- Acceptance suite: `tests/ToiNoMori.TownReadiness.Tests/`
- CI contract: `scripts/ci/check-stage6r11-contract.py`
- CI wrapper: `scripts/ci/run-stage6r11-town-readiness-ci.sh`
- Evidence writer: `scripts/ci/write-stage6r11-evidence.py`
- Workflow: `.github/workflows/stage6r11-town-readiness.yml`

## Acceptance IDs

- `TC-ACC-MVS01-082-TR` — stable Question reference
- `TC-ACC-MVS01-083-TR` — Published-only public read boundary
- `TC-ACC-MVS01-084-TR` — public DTO non-leakage allowlist
- `TC-ACC-MVS01-085-TR` — Town role does not imply Forest role
- `TC-ACC-MVS01-086-TR` — withdrawal removes public body while retaining lifecycle record

## Full gate

Expected native regression after this Stage:

- Domain: 12
- API: 41
- Mobile: 7
- OIDC E2E: 8
- Town readiness: 5
- PostgreSQL: 12
- DR: 5
- Total: 90

Final GitHub Actions evidence is recorded separately after the exact final HEAD is GREEN.
