# AI collaboration governance

| 役割 | 責務 | 単独でしてはいけないこと |
|---|---|---|
| Codex | 実装、test、evidence、Findingへの技術応答 | 自分の実装を最終受入する |
| Claude | 独立review、反証、境界不整合の指摘 | 無承認の実装・merge・Finding CLOSE |
| User | 優先順位、policy判断、最終受入 | なし（repository owner） |
| GitHub Actions | 再現可能な機械gate | 意味論・policyの最終判断 |

## 標準サイクル

1. UserがStage scopeを承認
2. CodexがDraft PRへ実装
3. GitHub Actionsが固定gateを実行
4. Claudeが固定SHAをreview
5. CodexがFindingごとに応答・是正
6. Claudeが再検証
7. Userがaccept / defer / rejectを判断
8. mergeはUserの明示承認後

AI同士の合意はユーザーの最終受入を代替しません。
