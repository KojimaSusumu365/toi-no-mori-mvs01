# GitHub auto-drive Controller

Status: **BOOTSTRAP IMPLEMENTED — DISABLED**

Normative design: [QF-OPS-MVS01-001 Version 0.5.1](automation/QF-OPS-MVS01-001-v0.5.1.md)  
Freeze review: [QF-RVR-MVS01-014](automation/QF-RVR-MVS01-014-freeze-confirmation.md)

## Boundary

The Controller is a deterministic state machine around two AI workers. Codex may
manufacture a patch; Claude may review a fixed SHA. Neither agent can authorize a
Work Order, change the control plane, close its own Finding, merge, deploy or
start a Stage.

The current implementation is deliberately disabled because the Independent
Automation Release Reviewer appointment is `VACANT`, the existing stacked PRs
are unmerged, GitHub App/secrets are not configured, and external settings have
not completed Step 2.5 measurement.

## Implemented bootstrap surface

- six machine-readable registries under `.github/ai/registries/`;
- five JSON Schemas under `.github/ai/schemas/`;
- deterministic Work Order, scope, origin, CI, review, loop, evidence,
  disposition and role-appointment decisions in `scripts/ai_controller/`;
- provider-separated workflow definitions with full-SHA Action pins;
- `AUTO-T01` through `AUTO-T39` as 40 failure-first test cases;
- a content-addressed evidence layout and a threat baseline;
- a Draft-only dry-run path that uses no AI or write credential.

The Controller implementation and its automation acceptance tests are also
treated as Control Plane and are denied to normal manufacturing patches.

## Enablement gates

1. Merge the prerequisite PR chain through the accepted Stage 6R-11R head.
2. Appoint a different human as Independent Automation Release Reviewer. For
   the initial bootstrap, the trusted default branch does not yet contain the
   appointment Required Check, so `TC-ACC-MVS01-094-BOOTSTRAP`
   requires the independent human signature to be recorded against the fixed
   Controller implementation SHA before merge. Later appointments must pass the
   deterministic Required Check.
3. Run Step 2.5 measurements and replace every `NOT_MEASURED` value.
4. Have Codex execute all 40 tests on a fixed implementation SHA.
5. Have Claude technically review that same SHA.
6. Obtain the independent human signature and Organizer acceptance.
7. Merge this governance PR manually.
8. Configure secrets/App/rules only through a separately authorized operation.

Until all gates pass, `QF_AI_PHASE` must remain unset and every privileged route
must fail closed or return a credential-free no-op.

## Frozen-version compatibility backlog

`QF-OPS-MVS01-001 Version 0.5.1` remains byte-for-byte frozen and therefore
still states `AUTO-T01` through `AUTO-T38` / 39 cases. `AUTO-T39` was added as
the P3 implementation correction `AUTO-IMPL-P3-013`; the implemented and
operational acceptance count is 40. Reconcile this wording in the next
non-frozen specification revision without amending Version 0.5.1.
