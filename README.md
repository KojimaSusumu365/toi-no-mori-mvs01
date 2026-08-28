# 問いの森 CORE — MVS-01

「問いが仕事を生み、仕事が経験を生み、経験が次の問いを生む」ためのQuestion Forest最小実装です。AIは主人公ではなく、人間の問いを分解・接続・翻訳・可視化する支援役です。

## 現在地

- Stage 6R-1〜6R-11 cumulative implementation: Draft baseline
- Stage 6R-11R: **CLOSED — PASS_WITH_FINDINGS**
- Final manufacturing gates: Stage 6R-10 90/90, Stage 6R-11 90/90, navigation GREEN
- Claude independent review: `QF-RVR-MVS01-007`, no blocking Finding
- Stage 6R-12: NOT STARTED
- Virtual Town runtime: not implemented

正確なSHA、Run、Artifact、未完了条件は [CURRENT_STATE.md](CURRENT_STATE.md) を参照してください。

## 最初に読む文書

| 目的 | 正本 |
|---|---|
| 現在地 | [CURRENT_STATE.md](CURRENT_STATE.md) |
| Claudeとの協働規約 | [CLAUDE.md](CLAUDE.md) |
| アーキテクチャ境界 | [ARCHITECTURE.md](ARCHITECTURE.md) |
| 文書地図 | [docs/INDEX.md](docs/INDEX.md) |
| 次段階 | [ROADMAP.md](ROADMAP.md) |

## Repository structure

- src/: production code
- tests/: executable specifications
- scripts/: local and CI gates
- infra/: PostgreSQL and Sakura configuration
- docs/architecture/: ADR, UML, DR, identity and boundary contracts
- docs/governance/: source-of-truth, review and taxonomy rules
- docs/stages/stage6r01..12/: Stage manifests and specifications
- docs/reviews/stage6r11r/: Claude–Codex review packet
- docs/evidence/stage6r01..11/: execution evidence
- docs/archive/: non-current material preserved for provenance
- .github/workflows/: mandatory CI gates

The physical taxonomy is enforced by scripts/check-repository-taxonomy.py. Old flat Stage, ADR, UML, Manifest and Evidence paths are rejected by CI.

## Build and test

Main regression and organization gates:

    ./scripts/test-all.sh
    ./scripts/test-stage6r10-ci-contract.sh
    ./scripts/test-stage6r11-ci-contract.sh
    python3 scripts/check-repository-navigation.py
    python3 scripts/check-repository-taxonomy.py

## Safety and acceptance

- Forest and Town do not share a database.
- Town does not persist Question title or body.
- PostgreSQL application and migration roles remain NOBYPASSRLS.
- 404 hides absent versus withdrawn; 429/503/timeout/DNS remain unresolved.
- AI cannot merge, remove Draft status, close its own Finding or record final acceptance without explicit human authorization.
- A GREEN CI result is evidence for a fixed source identity, not production approval.

## Historical material

Earlier README and pre-6R documents are retained under [docs/archive/](docs/archive/) with explicit non-current labels. They are not deleted and are not normative.
