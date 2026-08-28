# Claude collaboration guide

This is the first authoritative document for Claude in this repository. Claude is asked to perform an independent review, not to merge or silently redesign the system.

## Reading order

1. [CURRENT_STATE.md](CURRENT_STATE.md)
2. [ARCHITECTURE.md](ARCHITECTURE.md)
3. [Documentation index](docs/INDEX.md)
4. [Source of truth](docs/governance/SOURCE-OF-TRUTH.md)
5. [Review protocol](docs/governance/REVIEW-PROTOCOL.md)
6. [Stage 6R-11R review request](docs/reviews/stage6r11r/review-request.md)
7. Target implementation, tests and [typed evidence](docs/evidence/stage6r11/stage6r11r-github-acceptance.md)

## Fixed implementation review target

- Repository: KojimaSusumu365/toi-no-mori-mvs01
- Implementation commit: 61b55e03d1c3df7355eb3cf15aa1f1fcad7870e1
- Implementation tree: 23de94ef1e6ded9e2122b11880b7cb80ff8378ae
- Draft implementation PR: #4
- Stage 6R-11R: CLOSED as `PASS_WITH_FINDINGS`; review `QF-RVR-MVS01-007`
- Stage 6R-12: NOT STARTED

The physical-taxonomy branch relocates documents without changing this implementation target. Do not substitute the taxonomy commit, tree object, PR merge ref or workflow checkout SHA for the fixed implementation commit.

## Invariant boundaries

- Question Forest is upstream; Virtual Town is downstream.
- Forest and Town do not share a database.
- Question is a Forest Entity; Town Aggregate Root is Task.
- Town never persists the Question body or title.
- The existing UUID is an opaque reference.
- Forest 404 does not distinguish absent from withdrawn.
- 429, 503, timeout and DNS failure do not mean withdrawal.
- Humans retain final judgment, approval and responsibility.

See the frozen [Forest–Town contract](docs/architecture/contracts/forest-town-boundary-v1.md).

## Requested review behavior

- Fix the target SHA before reading.
- Separate confirmed facts, inference and questions.
- Report inconsistencies as Findings with path and target SHA.
- Re-verify existing Findings independently.
- Use the [review protocol](docs/governance/REVIEW-PROTOCOL.md).

## Prohibited without explicit user authorization

Do not push to main, merge a PR, remove Draft status, delete a branch, weaken RLS/audit boundaries, self-close a Finding, start Stage 6R-12 or deploy a real environment.
