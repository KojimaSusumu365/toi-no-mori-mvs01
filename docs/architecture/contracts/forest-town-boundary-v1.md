# Forest–Town boundary contract v1

Status: **FROZEN FOR STAGE 6R-11R REVIEW**

## Four rules

1. **No shared database.** Forest owns Question; Town owns Task.
2. **Versioned integration API.** A future Town adapter must use an explicit version such as `/api/v1/...`. The existing unversioned MVS Public Read route is not permission to create an unversioned Town dependency.
3. **Opaque reference.** Canonical `question_ref` is the existing Question UUID. Display aliases must not be used in APIs, search identity, or `context_ref`.
4. **Stable error semantics.** Forest keeps withdrawn and absent indistinguishable as 404.

## Town resolution state

| Observation | Town result |
|---|---|
| prior successful 200 + current 404 | `unavailable` |
| 429 | `unresolved:rate_limited` |
| 503 | `unresolved:temporary_failure` |
| timeout / DNS / connection refusal / TLS failure | `unresolved` |
| no prior 200 + 404 | invisible/absent; do not infer prior existence |

## Storage contract

Town may persist only `question_ref`, `last_resolved_at`, `last_resolution_status`, and `source_api_version`. Forest-derived `title` and `body` must not be persisted by Town. Temporary display caches require a finite TTL; stale-if-error must not extend content indefinitely. The first TTL value is a VT-1 decision.

## Tenant Architecture Gate

Current anonymous Public Read exposes exactly one configured tenant. UUID-only external references depend on this fact. A second effective public tenant must make startup/CI fail until the System Architect approves a tenant-context source. The gate reads configuration; it must not add a BYPASSRLS role or aggregate across protected business rows.

## Audience deferral

`TC-ACC-MVS01-087-OIDC` is registered as not-run until VT-1 creates a real second audience. A fabricated second production audience is outside Stage 6R-11R.
