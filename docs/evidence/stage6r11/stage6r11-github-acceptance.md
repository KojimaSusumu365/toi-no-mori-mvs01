# Stage 6R-11 — GitHub Acceptance Evidence

## Verdict

**ACCEPTED for the functional implementation head recorded below.**

Stage 6R-11 validates Question Forest Minimum / Town Readiness. It does not implement Virtual Town runtime code.

The acceptance result demonstrates that the existing Question Forest behavior already satisfies the five readiness boundaries while preserving the complete Stage 6R-10 regression baseline.

## Functional implementation under test

- Repository: `KojimaSusumu365/toi-no-mori-mvs01`
- PR: `#1`
- Branch: `stage6r4c-postgresql-green-fix`
- Functional head SHA: `07815c1a9b22c437c72a991fe120a1f8be61bc9e`
- Pull-request merge ref SHA used by GitHub Actions: `c0cd20c963bd7a4f236ae538034a85fd94e469e8`
- Workflow: `Stage 6R-11 Question Forest Minimum Town Readiness`
- Run ID: `33002454524`
- Job ID: `98287520363`
- Run attempt: `1`
- Result: `success`

## Immutable workflow artifact

- Artifact ID: `9619241730`
- Artifact name: `stage6r11-town-readiness-evidence-33002454524-1`
- GitHub artifact digest: `sha256:ae9e0218c4165d57b4ac5c460a88087d8554bf8468747536a712c3a158d75a2b`
- Full-regression log SHA-256: `5c796a5f6672a81f89f53885a09c8c14917b6c87ac2adf9adf7cee1f5d2d6010`
- Evidence status: `accepted`
- Gate exit code: `0`

## Native regression result

| Suite | Result |
|---|---:|
| Domain | 12 / 12 |
| API | 41 / 41 |
| Mobile | 7 / 7 |
| OIDC browser E2E | 8 / 8 |
| Stage 6R-11 Town readiness | 5 / 5 |
| PostgreSQL | 12 / 12 |
| DR | 5 / 5 |
| **Total** | **90 / 90** |

Additional acceptance gates:

- test ID uniqueness: GREEN
- Release build warnings: 0
- Release build errors: 0
- non-root runner: GREEN
- native execution: GREEN
- final fail-closed evidence gate: GREEN

## Town-readiness acceptance cases

- `TC-ACC-MVS01-082-TR` — Stable Question reference: GREEN
- `TC-ACC-MVS01-083-TR` — Published-only public read boundary: GREEN
- `TC-ACC-MVS01-084-TR` — Public DTO non-leakage allowlist: GREEN
- `TC-ACC-MVS01-085-TR` — Unknown `TownAdmin` does not imply Forest role: GREEN
- `TC-ACC-MVS01-086-TR` — Withdrawn Question disappears from public read while lifecycle record remains: GREEN

## Architecture conclusion

For the scope measured by Stage 6R-11:

> **Virtual Town対応のためのQuestion Forest CORE変更は不要。**

The existing Question Forest can remain the upstream Domain. A future Virtual Town should attach through an adapter/integration boundary and refer to a Forest Question by a stable external reference equivalent to `context_ref`; it should not move Town Task/Project/Ledger responsibilities into Question Forest CORE.

No production Question Forest CORE code was changed to obtain this readiness result. The Stage adds specification/test/CI evidence around behavior already present in the system.

## Scope and limitations

This evidence proves the current readiness contract only. It does **not** prove or implement:

- Virtual Town runtime
- Town Task / Project persistence
- Experience Ledger
- Integration Gateway runtime
- Town cache or tombstone runtime
- Citizen Compute
- physical Sakura Cloud Tokyo–Ishikari region failover

The DR suite remains the established Stage 6R-10 native local dual-PostgreSQL role-switch/recovery test and must not be described as a live physical inter-region failover exercise.

For deletion lifecycle, Stage 6R-11 verifies current `Withdrawn` behavior: the stable Question ID remains in the Forest lifecycle record while the Question body is no longer available through the public API. Physical delete/tombstone behavior is intentionally deferred to the future integration stage.

## Evidence-seal rule

This document records the exact functional implementation run above. Because committing the document itself changes the branch HEAD, the resulting documentation-only evidence-seal HEAD must pass the same Stage 6R-11 workflow before the PR is considered fully accepted. That later verification does not alter the functional evidence recorded here.
