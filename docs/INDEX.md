# Documentation index

## 15分で現在地を理解する

1. [Current state](../CURRENT_STATE.md)
2. [Architecture](../ARCHITECTURE.md)
3. [Stage 6R-11 manifest](../MANIFEST-STAGE6R11.md)
4. [Town readiness](stage6r11-town-readiness.md)
5. [Source of truth](governance/SOURCE-OF-TRUTH.md)
6. [Stage 6R-11R review request](reviews/stage6r11r/review-request.md)

## 読者別

| 読者 | 開始点 |
|---|---|
| Claude / 外部reviewer | [CLAUDE.md](../CLAUDE.md) |
| 実装担当 | [Architecture](../ARCHITECTURE.md) → `src/` → `tests/` |
| CI担当 | `.github/workflows/` → `scripts/ci/` → `docs/evidence/` |
| 受入判断者 | [Current state](../CURRENT_STATE.md) → [review packet](reviews/stage6r11r/) |
| 将来計画 | [Roadmap](../ROADMAP.md) |

## 既存成果物の扱い

- rootの `MANIFEST-*.md` は各Stageの凍結目録として移動しない
- `docs/evidence/` は実行証跡。設計文書と混ぜない
- `docs/reviews/` はAI間の依頼・所見・応答・最終受入
- `src/`、`tests/`、`scripts/` の既存pathはこの整理Stageで変更しない

## Stage 6R-11R closure

- [Closure ledger](stage6r11r-closure.md)
- [Exact GitHub acceptance evidence](evidence/stage6r11r-github-acceptance.md)
- [Forest–Town boundary contract](forest-town-boundary-v1.md)
- [Planned-to-actual Test ID mapping](stage6r11r-test-id-mapping.md)
- [Deferred test registry](../spec/deferred-tests.json)
