# Bootstrap measurement and readiness evidence

This directory carries the read-only evidence used to assess the initial
Controller bootstrap sequence. It contains no secret or variable values.

## QF-MEP-MVS01-001-MR1 — Step 2.5 partial measurement

- [Human-readable result](QF-MEP-MVS01-001-step2.5-read-only-result.md)
- [Typed evidence](QF-MEP-MVS01-001-step2.5-read-only-evidence.json)
- Decision: `PARTIAL / NOT AUTHORIZED TO ENABLE`
- P3-015: `OPEN / DEFERRED`

## QF-MEP-MVS01-001-BMA1 — bootstrap merge assessment

- [Human-readable assessment](QF-MEP-MVS01-001-bootstrap-merge-assessment.md)
- [Typed evidence](QF-MEP-MVS01-001-bootstrap-merge-evidence.json)
- Decision: `NO_GO_NOW / CONDITIONAL_BOOTSTRAP_CANDIDATE`

## QF-MEP-MVS01-001-OSA1 — Repository Owner settings attestation

- [Human-readable attestation](QF-MEP-MVS01-001-owner-settings-attestation.md)
- [Typed evidence](QF-MEP-MVS01-001-owner-settings-evidence.json)
- Owner settings checkpoint: `MEASURED_COMPLETE`
- Secret values: not displayed or retrieved
- Variable values: not extracted or recorded

## Source integrity

| File | SHA-256 |
|---|---|
| `QF-MEP-MVS01-001-step2.5-read-only-result.md` | `d826f2b29af4a6b22afa8311ef479afec55d83997e1758e56c4a9e94bc7b6ea6` |
| `QF-MEP-MVS01-001-step2.5-read-only-evidence.json` | `0c399a48aee2556c8c824c3aa5bee9c95806e578da45ef480528246e09d6f9fb` |
| `QF-MEP-MVS01-001-bootstrap-merge-assessment.md` | `4e686e058af902966aec97e55ca7bd915c7af871e001c5dd58b4eb995b694d3d` |
| `QF-MEP-MVS01-001-bootstrap-merge-evidence.json` | `4e554855551c8747eda08c0b9848cb31ccb8dd45670b277d8024d2ec7ecbf47f` |
| `QF-MEP-MVS01-001-owner-settings-attestation.md` | `8723af35ff64c2b35b66aff948750ff07d54045779b0db13c78b75f3cf568f83` |
| `QF-MEP-MVS01-001-owner-settings-evidence.json` | `5016fffe561756f7f3386dfabf066adeb3d91d62542756ee64cd7fc8cbe9496d` |

The hashes identify the source files before this Draft publication candidate.
The evidence becomes durable only after human review and merge to the default
branch. Draft publication does not make the records authoritative.

## Preserved boundary

- PR #1 and #3 through #7 remain Draft, open and unmerged.
- PR retargeting and branch updates are not authorized by these records.
- The Controller remains `BOOTSTRAP_DISABLED`.
- The reviewer appointment remains `VACANT / PENDING ACTIVATION`.
- P3-015 remains `OPEN / DEFERRED`.
- Secrets, variables, GitHub Apps, rulesets and branch protection are unchanged.
- Phase A and Stage 6R-12 are not started.
