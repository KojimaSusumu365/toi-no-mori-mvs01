# Stage 6R-11R review request for Claude

Status: **READY_FOR_CLAUDE_REVIEW**  
Target implementation commit: `61b55e03d1c3df7355eb3cf15aa1f1fcad7870e1`  
Target implementation tree: `23de94ef1e6ded9e2122b11880b7cb80ff8378ae`  
Draft PR: [#4](https://github.com/KojimaSusumu365/toi-no-mori-mvs01/pull/4)  
Base: `stage-gh-org-0-claude-onboarding@60c10feb1fed4b4b5000fac4145aa4def591877f`  
Evaluated PR merge ref: `83857ee48d4f5317dddf0023a8821a67e3e62980`

## Requested decision

Independently verify whether RVR-N10 through RVR-N13 and the listed P0 closure conditions are satisfied by the target commit. Do not treat 90/90 alone as proof. Return Findings using [REVIEW-PROTOCOL.md](../../governance/REVIEW-PROTOCOL.md).

## Review order

1. Read [GitHub acceptance evidence](../../evidence/stage6r11r-github-acceptance.md).
2. Inspect the target implementation commit, not the later documentation-seal commit.
3. Inspect [Forest–Town boundary](../../forest-town-boundary-v1.md).
4. Inspect [Test ID mapping](../../stage6r11r-test-id-mapping.md) and `spec/deferred-tests.json`.
5. Re-evaluate the known Findings below.
6. Record the response in [claude-findings.md](claude-findings.md).

## Known Findings to re-evaluate

| ID | Implemented closure claim | Required independent check |
|---|---|---|
| RVR-N10 | evidence totals are computed from registered suites; missing Town suite is a failing synthetic contract; fixed total key removed | inspect writer, contract checker, workflow and artifact identity |
| RVR-N11 | tested merge ref, authoritative branch HEAD, base SHA and relationship are separately typed | verify Run `33135504039`, Job `98734412669`, target HEAD and merge ref |
| RVR-N12 | all four RLS policies already use `NULLIF(current_setting(...), '')::uuid`; `TC-ACC-MVS01-067-PG` covers empty GUC and pool reuse | inspect migrations and PostgreSQL test path |
| RVR-N13 | Public Read uses a validated single-tenant configuration gate; no row-crossing query and no `BYPASSRLS` role were added | inspect `PublicReadTenantContext`, stores and startup-negative tests |

## P0 checks

- RV-010: approval is version-bound and stale review is rejected.
- RV-020: tenant/RLS boundary remains fail-closed.
- RV-030 and RVA-C01: real 401, 403 and 429 rejected paths produce auditable rows.
- RVA-C06: empty GUC and connection reuse remain safe.
- PostgreSQL 12/12 and DR 5/5 are part of the exact-head 90/90 run.
- Declared not-run tests have reason, owner and due; they are not counted as passed.

## Forest–Town contract checks

- no shared database;
- versioned future integration API;
- opaque UUID reference;
- withdrawn and absent remain indistinguishable 404;
- 429, 503, timeout, DNS and no response never become withdrawal;
- Town may persist references and resolution metadata, but not Question `title` or `body`;
- temporary cache has finite TTL;
- second public tenant stops startup until an approved tenant-context design exists.

## Non-scope

Virtual Town runtime, shared DB, new BYPASSRLS role, Stage 6R-12, merge, Draft removal and deployment are not authorized.

## Required output

For every Finding, include ID, severity, category, target SHA, claim, evidence paths, impact, required closure and confidence. Explicitly state one of:

- no blocking Finding for owner acceptance;
- blocking Finding(s) remain;
- policy decision required.

Reviewing this request does not authorize merge or final acceptance.
