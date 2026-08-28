# Stage 6R-11R GitHub acceptance evidence

Captured at: 2026-08-28T02:22:00Z  
Acceptance state: **ACCEPTED — STAGE 6R-11R PASS_WITH_FINDINGS**

## Source identity

| Type | Value |
|---|---|
| Repository | `KojimaSusumu365/toi-no-mori-mvs01` |
| Draft PR | [#4](https://github.com/KojimaSusumu365/toi-no-mori-mvs01/pull/4) |
| Authoritative branch | `stage6r11r-closure` |
| Implementation HEAD | `61b55e03d1c3df7355eb3cf15aa1f1fcad7870e1` |
| Implementation tree | `23de94ef1e6ded9e2122b11880b7cb80ff8378ae` |
| Base branch | `stage-gh-org-0-claude-onboarding` |
| Base commit | `60c10feb1fed4b4b5000fac4145aa4def591877f` |
| Evaluated PR merge ref | `83857ee48d4f5317dddf0023a8821a67e3e62980` |
| Relationship | `pull_request_merge_ref` |

The Stage 6R-11 workflow verified that the implementation HEAD is an ancestor of the evaluated PR merge ref. The merge ref is not mislabeled as the branch HEAD.

## Green runs

| Purpose | Workflow Run / attempt | Job | Result | Artifact |
|---|---|---|---|---|
| Repository navigation | `33135504210 / 1` | `98734413111` — Human and AI onboarding contract | success | none |
| Stage 6R-10 cumulative regression | `33135504027 / 1` | `98734412535` — Native Tokyo-Ishikari DR evidence regression 85/85 gate | 85/85 | `9671915364` |
| Stage 6R-11 Town readiness | `33135504039 / 1` | `98734412669` — Question Forest Minimum Town readiness 90/90 gate | 90/90 | `9671907000` |

### Stage 6R-11 artifact

- Name: `stage6r11-town-readiness-evidence-33135504039-1`
- Digest: `sha256:fd4bd48943a0d9c6fb4f3fb20622856503f2f2783070da2070f3cd85878a1955`
- Size: 8,693 bytes
- Retention expiry: 2026-09-27T02:20:54Z
- Suite results: Domain 12, API 41, Mobile 7, OIDC 8, PostgreSQL 12, DR 5, Town Readiness 5
- Total: expected 90, passed 90, failed 0, gate not-run 0

### Stage 6R-10 artifact

- Name: `stage6r10-tokyo-ishikari-dr-evidence-33135504027-1`
- Digest: `sha256:9016ac324d339ece6e10027e78acacb5b459d74593030b12d98563070f9ce13e`
- Size: 8,398 bytes
- Retention expiry: 2026-09-27T02:21:20Z
- Total: expected 85, passed 85, failed 0

## Declared but not counted as passed

| Test ID | State | Reason / owner / due |
|---|---|---|
| `TC-PERF-MVS01-002-PG` | not-run | Production-like 100,000-row execution has not run / Performance Owner / before pilot Gate G3 or before the public dataset reaches 100,000 rows, whichever comes first |
| `TC-ACC-MVS01-087-OIDC` | not-run | Cross-audience Town client does not exist before VT-1 / System Architect / VT-1 start |

These are governance declarations, not hidden passes inside the 90/90 total.
The authoritative values are machine-enforced by `spec/deferred-tests.json`.

## Taxonomy overlay Runs

The following successful Runs evaluate taxonomy commit
`80090e2eb56c4ddf438867572f8f6e8c389813ba` and close the overlay-ledger gap
identified as RVR-N18.

| Purpose | Run / attempt | Job | Result | Artifact |
|---|---|---|---|---|
| Stage 6R-10 historical evidence writer | `33139913725 / 1` | `98748216295` | 85/85 success | `9673573611`, `sha256:02922ab03a11eec9dab41141fff03dc7b996f53542b9762a3a0d7330f61ee155` |
| Repository navigation, taxonomy and links | `33139913729 / 1` | `98748216209` | success | none |
| Stage 6R-11 Town readiness | `33139913757 / 1` | `98748216596` | 90/90 success | `9673576028`, `sha256:16c7a9e3f6b52674eaec601a5ac70f41a173c7af59ab01743ff85f6dcccc3ea8` |

GitHub reported all three jobs completed successfully and both artifacts identified
the exact overlay head. Artifact expiry does not invalidate the recorded Run, Job,
commit and digest identity.

## Final Closure Runs

The final response commit is
`497d786fe687069c004b89b86b2b9345faeb9726`, tree
`ba3711b6597013df8b268dc764098e7ed68681e6`. PR #6 evaluated merge ref
`51e02a0488fbfdfaef3e26c05cc421e999e6d41d`, whose parents are the PR #5 base
`80090e2eb56c4ddf438867572f8f6e8c389813ba` and the response head.

| Purpose | Run / attempt | Job | Result | Artifact |
|---|---|---|---|---|
| Repository navigation, taxonomy and links | `33152117524 / 1` | `98786286113` | success | none |
| Stage 6R-10 cumulative and DR | `33152117623 / 1` | `98786286856` | 90/90 success | `9678180236`, `sha256:44a0d252b572123c68afc43d4f7cad85083d0951815fa9638066f483d80a6261` |
| Stage 6R-11 Town readiness | `33152117552 / 1` | `98786286664` | 90/90 success | `9678188675`, `sha256:3a04014251c64cf3ee5c69660c21697cdce45fd8848a08bfa95b44d477fd0b1e` |

Direct artifact inspection confirmed both native records contain seven suites,
`expectedTotal = passedTotal = executedTotal = 90`, `failedTotal = 0`, complete
suite registration, matching totals, unique Test IDs, clean builds and non-root
execution. The Stage 6R-11 record additionally contains the tested tree, both
merge-ref parents and `authoritativeHeadIncluded = true`.

## Superseded run

Run `33135291006` on implementation commit `756d7449769e5e27b891a28ba34d2212ed9b4c32` failed because the native DR harness launched the application without an explicit Public Read tenant. Commit `61b55e03d1c3df7355eb3cf15aa1f1fcad7870e1` added that explicit test configuration. The failed run is not used as acceptance evidence; the exact corrected HEAD was rerun and passed.

## Limits of this evidence

This record proves the implementation and registered regressions were green for
the typed identities above. Claude review and repository-owner acceptance are
separately recorded in the Stage 6R-11R review packet. It does not constitute merge
approval, production deployment or Stage 6R-12 authorization.
