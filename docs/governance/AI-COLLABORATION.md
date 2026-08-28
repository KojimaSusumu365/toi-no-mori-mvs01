# AI collaboration governance

Normative automation design: [QF-OPS-MVS01-001 v0.5.1](automation/QF-OPS-MVS01-001-v0.5.1.md)
Controller status: **BOOTSTRAP_DISABLED**

| 役割 | 責務 | 単独でしてはいけないこと |
|---|---|---|
| Codex | 実装、test、evidence、Findingへの技術応答 | 自分の実装を最終受入する |
| Claude | 独立review、反証、境界不整合の指摘 | 無承認の実装・merge・Finding CLOSE |
| Organizer | 問い、優先順位、Work Order、人間処置、残存risk、最終受入 | AIへ最終判断を委譲する |
| Controller | identity、scope、budget、状態遷移、停止条件の決定論的強制 | 意味論判断、merge、Stage開始 |
| Independent Automation Release Reviewer | 自動化基盤の第二人間検査 | Organizerまたは製造担当との兼任 |
| GitHub Actions | 再現可能な機械gate | 意味論・policyの最終判断 |

## 標準サイクル

1. OrganizerがQuestionからWork Orderを承認しdefault branchへmerge
2. ControllerがWork Order、origin、scope、risk、budgetを検証
3. Codexがpatchを製造し、資格情報なしのJobが製造試験を実行
4. publisherが同一hashのpatchだけをDraft PRへ追加
5. 決定論的CI Gateが実在Check Runを固定SHAで照合
6. Claudeが固定commit/treeを読取専用で技術review
7. Review GateがSchema、mode、identity、Finding境界を検証
8. OrganizerがFindingごとにaccept / defer / reject / closeを別Recordへ記録
9. mergeはOrganizerの明示承認後

Phase Aの自動是正は0回です。AI同士の合意はOrganizerの最終受入を代替しません。
Independent Automation Release Reviewerが`VACANT`の間は、実装、静的検査、
local／隔離sandbox試験だけが許され、governance PRのmerge、AI資格情報run、
Phase A開始は禁止です。
