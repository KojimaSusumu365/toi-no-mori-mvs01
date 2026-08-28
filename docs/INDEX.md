# Documentation index

## 15分で現在地を理解する

1. [Current state](../CURRENT_STATE.md)
2. [Architecture boundaries](../ARCHITECTURE.md)
3. [Repository taxonomy](governance/REPOSITORY-TAXONOMY.md)
4. [Stage 6R-11 manifest](stages/stage6r11/MANIFEST-STAGE6R11.md)
5. [Stage 6R-11 Town readiness](stages/stage6r11/stage6r11-town-readiness.md)
6. [Stage 6R-11R review request](reviews/stage6r11r/review-request.md)
7. [GitHub auto-drive Controller](governance/GITHUB-AUTODRIVE-CONTROLLER.md)

## Area index

| Area | Contents |
|---|---|
| [Architecture](architecture/README.md) | ADR, UML, DR, identity, runtime and Forest–Town contracts |
| [Governance](governance/REPOSITORY-TAXONOMY.md) | AI collaboration, source identity, review protocol and test traceability |
| [Stages](stages/README.md) | Stage 6R-01〜6R-12 manifests and specifications |
| [Reviews](reviews/README.md) | Claude findings, Codex responses and owner acceptance |
| [Evidence](evidence/README.md) | Stage-scoped execution evidence |
| [Archive](archive/README.md) | non-current material retained for provenance |
| [Automation review](reviews/automation/README.md) | Controller implementation review packets |
| [Automation evidence](evidence/automation/README.md) | content-addressed Review Results and dispositions |

## Current review packet

- [Review request](reviews/stage6r11r/review-request.md)
- [Review manifest](reviews/stage6r11r/review-manifest.json)
- [Claude findings](reviews/stage6r11r/claude-findings.md)
- [Codex response](reviews/stage6r11r/codex-response.md)
- [Closure ledger](reviews/stage6r11r/closure.md)
- [Test ID mapping](reviews/stage6r11r/test-id-mapping.md)
- [Final acceptance](reviews/stage6r11r/final-acceptance.md)

## Rules

- Root is reserved for onboarding and build entry points.
- Stage manifests and specifications live under their zero-padded Stage folder.
- Evidence is grouped by the Stage that generated or accepted it.
- Archive content is historical and never silently treated as current.
- CI rejects broken local Markdown links and legacy flat taxonomy paths.
