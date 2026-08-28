# Stage 6R-11R — Planned to actual Test ID mapping

## Verdict

The original increment planned 22 tests. Twenty-one planned IDs have native behavioural replacements with the same ID. The approved supplemental `TC-ACC-MVS01-066-API` explains the API +1, while `TC-PERF-MVS01-002-PG` remains explicitly not-run. Five Town Readiness tests were added later.

| Planned Test ID | Actual Test ID | Status | Split / merge / move reason |
|---|---|---|---|
| `TC-ACC-MVS01-063-DOM` | `TC-ACC-MVS01-063-DOM` | GREEN | same behavioural contract |
| `TC-ACC-MVS01-064-API` | `TC-ACC-MVS01-064-API` | GREEN | same behavioural contract |
| `TC-ACC-MVS01-065-API` | `TC-ACC-MVS01-065-API` | GREEN | same behavioural contract |
| `TC-ACC-MVS01-066-PG` | `TC-ACC-MVS01-066-PG` | GREEN | same behavioural contract |
| `TC-ACC-MVS01-067-PG` | `TC-ACC-MVS01-067-PG` | GREEN | same behavioural contract |
| `TC-ACC-MVS01-068-PG` | `TC-ACC-MVS01-068-PG` | GREEN | same behavioural contract |
| `TC-ACC-MVS01-069-API` | `TC-ACC-MVS01-069-API` | GREEN | same behavioural contract |
| `TC-ACC-MVS01-070-API` | `TC-ACC-MVS01-070-API` | GREEN | same behavioural contract |
| `TC-ACC-MVS01-071-API` | `TC-ACC-MVS01-071-API` | GREEN | same behavioural contract |
| `TC-ACC-MVS01-071-PG` | `TC-ACC-MVS01-071-PG` | GREEN | same behavioural contract |
| `TC-ACC-MVS01-072-API` | `TC-ACC-MVS01-072-API` | GREEN | same behavioural contract |
| `TC-ACC-MVS01-073-PG` | `TC-ACC-MVS01-073-PG` | GREEN | same behavioural contract |
| `TC-ACC-MVS01-074-PG` | `TC-ACC-MVS01-074-PG` | GREEN | same behavioural contract |
| `TC-ACC-MVS01-075-PG` | `TC-ACC-MVS01-075-PG` | GREEN | same behavioural contract |
| `TC-ACC-MVS01-076-MOB` | `TC-ACC-MVS01-076-MOB` | GREEN | same behavioural contract |
| `TC-ACC-MVS01-077-OIDC` | `TC-ACC-MVS01-077-OIDC` | GREEN | same behavioural contract |
| `TC-ACC-MVS01-078-DR` | `TC-ACC-MVS01-078-DR` | GREEN | same behavioural contract |
| `TC-ACC-MVS01-079-DOM` | `TC-ACC-MVS01-079-DOM` | GREEN | same behavioural contract |
| `TC-ACC-MVS01-080-API` | `TC-ACC-MVS01-080-API` | GREEN | same behavioural contract |
| `TC-ACC-MVS01-081-DOM` | `TC-ACC-MVS01-081-DOM` | GREEN | same behavioural contract |
| `TC-ACC-MVS01-081-API` | `TC-ACC-MVS01-081-API` | GREEN | same behavioural contract |
| `TC-PERF-MVS01-002-PG` | — | not-run | Owner: Performance Engineer. A production-like 100,000-row baseline is not yet available. Due: before pilot Gate G3 or before the public dataset reaches 100,000 rows, whichever comes first. |

## Approved supplemental test

| Plan relationship | Actual Test ID | Status | Reason |
|---|---|---|---|
| supports `TC-ACC-MVS01-066-PG` | `TC-ACC-MVS01-066-API` | GREEN | Production configuration can reject role/connection collapse before a database connection; it does not replace the PostgreSQL test. |

## Town Readiness addition

| Requirement | Actual Test ID | Status |
|---|---|---|
| Stable UUID reference | `TC-ACC-MVS01-082-TR` | GREEN |
| Published-only Public Read | `TC-ACC-MVS01-083-TR` | GREEN |
| Public DTO allowlist | `TC-ACC-MVS01-084-TR` | GREEN |
| Town role independence | `TC-ACC-MVS01-085-TR` | GREEN |
| Withdrawn/absent public 404 | `TC-ACC-MVS01-086-TR` | GREEN |

## Count reconciliation

| Layer | Planned | Actual increment | Explanation |
|---|---:|---:|---|
| Domain | 3 | 3 | same IDs |
| API/BFF | 8 | 9 | approved supplemental 066-API |
| PostgreSQL | 7 | 7 | same IDs |
| Performance | 1 | 0 | formally not-run above |
| Mobile | 1 | 1 | same ID |
| OIDC E2E | 1 | 1 | same ID |
| DR | 1 | 1 | same ID |
| Town Readiness | 0 | 5 | later boundary suite |
| **Total** | **22 + 5** | **27** | reconciled without treating not-run as passed |
