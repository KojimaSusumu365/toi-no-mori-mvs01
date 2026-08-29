# External and automated review protocol

## 1. Authority boundary

Codex manufactures and verifies. Claude reviews a fixed commit and tree. The
Organizer alone selects scope, records dispositions, accepts residual risk and
authorizes merge or Stage transition. AI agreement is not acceptance.

The normative automation design is [QF-OPS-MVS01-001 v0.5.1](automation/QF-OPS-MVS01-001-v0.5.1.md).
The Controller remains `BOOTSTRAP_DISABLED` while the Independent Automation
Release Reviewer appointment is `VACANT`.

## 2. Review identity

Every request fixes repository, PR, Work Order, head commit, tree, default-branch
control plane, expected review mode and canonical Review Request SHA-256. A
changed head or unapproved base drift makes the result stale.

`INITIAL` is the default. `REVERIFY` is valid only when a prior Review Result is
already a content-addressed record on the default branch, the head changed, its
Finding was `OPEN`, and a matching current-SHA `FIX_CANDIDATE` exists. Claude
echoes the selected mode; it never chooses it.

## 3. Finding format

Automated reviews use `technical-review.schema.json@3` and IDs from
`.github/ai/registries/finding-ids.yml`.

| Field | Values / rule |
|---|---|
| `severity` | `P0`, `P1`, `P2`, `P3` |
| `verification_status` from Claude | `OPEN`; or `VERIFIED` only in valid `REVERIFY` |
| `disposition` from Claude | `UNDECIDED` only |
| evidence | path, verified fact, risk, required change and residual risk |

Non-actionable observations belong in `notes[]`; actionable notes are P3
Findings. Duplicate or unknown IDs are rejected.

## 4. Status and disposition owners

| Value | Owner | Constraint |
|---|---|---|
| `OPEN` | Claude / Gate | initial or unresolved Finding |
| `FIX_CANDIDATE` | Codex | new candidate SHA and patch hash required |
| `VERIFIED` | Claude | valid `REVERIFY` at the new SHA only |
| `CLOSED` | Organizer | verified evidence and residual-risk acceptance required |
| `UNDECIDED` | Claude | only disposition Claude may emit |
| `ACCEPTED_PLAN` | Organizer | correction plan accepted |
| `REJECTED_WITH_REASON` | Organizer | reason recorded |
| `DEFERRED` | Organizer | owner, reason and due date required |
| `POLICY_DECISION_REQUIRED` | Organizer | policy owner and decision due date required |

Codex cannot self-verify or close a Finding. Claude cannot set an Organizer
disposition or close a Finding. Organizer decisions are append-only separate
records and never rewrite Claude's Review Result.

## 5. Durable records

Review Results are canonical JSON at
`docs/evidence/automation/reviews/<reviewed-sha>/<content-sha256>.json`.
Organizer dispositions are at
`docs/evidence/automation/dispositions/<review-content-sha256>/<record-sha256>.json`.
Both are append-only. Actions Artifacts and logs are transport evidence, not the
long-term source of truth.

## 6. Compatibility mapping

| Legacy expression | QF-AI-COLLAB-v5 expression |
|---|---|
| `Note` | non-actionable `notes[]`; otherwise `severity: P3` |
| `ACCEPTED` | `disposition: ACCEPTED_PLAN` |
| `REJECTED_WITH_REASON` | same-named disposition |
| `DEFERRED_WITH_OWNER_REASON_DUE` | `disposition: DEFERRED` plus owner/reason/due |
| `POLICY_DECISION_REQUIRED` | same-named disposition |
| `CLOSED_VERIFIED` | Organizer `CLOSED` backed by Claude `VERIFIED` evidence |

Historical Stage 6R-11R records retain their original vocabulary. This mapping
preserves their meaning without altering frozen evidence.

## 7. Acceptance gate

- all registry-defined checks executed with evidence;
- required CI names, Work Order set and actual successful Check Runs match at
  the reviewed SHA;
- no unresolved P0/P1 Finding;
- every deferred item has owner, reason and due condition;
- Review Result is durable and content-addressed;
- Independent Automation Release Reviewer check is current-head GREEN for the
  automation governance PR;
- Organizer records final acceptance explicitly.

Merge, Draft removal, deployment and Stage start are never Controller outputs.
