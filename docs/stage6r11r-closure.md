# Stage 6R-11R closure ledger

Status: **IMPLEMENTED — AWAITING EXACT-HEAD CI AND CLAUDE REVIEW**

## A. Evidence Gate

- [x] Town Readiness is a registered 5-test suite in the machine evidence.
- [x] Total is computed from registered suite metadata.
- [x] A missing suite rejects the synthetic contract test.
- [x] Fixed key `nativeTotal90Of90` is removed.
- [x] tested commit, branch head, base commit, relationship, workflow, Run and attempt are typed evidence fields.
- [ ] Exact implementation HEAD Run/Job/artifact is recorded after CI.

## B. Forest–Town boundary

- [x] DB sharing prohibited.
- [x] Future Town integration API must be versioned.
- [x] Canonical opaque reference is the existing UUID.
- [x] withdrawn and absent remain 404.
- [x] prior 200 + current 404 means Town `unavailable`.
- [x] 429/503/no response remain `unresolved`.
- [x] Town persistence of `title` and `body` prohibited.
- [x] temporary cache requires finite TTL.
- [x] Public Read is single-tenant by validated configuration.
- [x] second public tenant activates a fail-closed Architecture Gate.
- [x] `TC-ACC-MVS01-087-OIDC` registered as not-run until VT-1.

## C. P0 closure evidence

| Finding | Evidence | State |
|---|---|---|
| RV-010 version-bound approval | `TC-ACC-MVS01-063-DOM`, `064-API`, `077-OIDC` | implemented; CI pending |
| RV-020 tenant/RLS boundary | `TC-ACC-MVS01-065-API`, `066/067/068-PG` | implemented; CI pending |
| RV-030 rejection audit | middleware wraps Authentication/Authorization; `TC-ACC-MVS01-071-API` now retrieves 401/403/429 rows | implemented; CI pending |
| RVA-C01 outer audit envelope | `TC-ACC-MVS01-071-API` actual rejected paths | implemented; CI pending |
| RVA-C06 empty GUC/pool reuse | `TC-ACC-MVS01-067-PG`; four RLS policies use `NULLIF` | implemented; CI pending |
| PostgreSQL | expected 12/12 | CI pending |
| DR | expected 5/5 | CI pending |

## D. Traceability

- [x] Original 22 planned IDs mapped to actual results.
- [x] API +1 explained by approved supplemental `066-API`.
- [x] `TC-PERF-MVS01-002-PG` is not counted as passed and has owner/reason/due.
- [x] Town 5 IDs mapped.
- [x] duplicate modern IDs and deferred/executable collision rejected by CI.

## Remaining external actions

1. Record exact-head CI evidence.
2. Claude reviews the fixed implementation SHA and returns Findings.
3. Codex responds by Finding ID.
4. Repository owner records final acceptance.

This file must not be changed to PASS by an implementing AI alone.
