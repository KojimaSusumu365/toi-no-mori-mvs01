# Controller修正版R1 Claude REVERIFY依頼

- 文書ID: QF-RRQ-MVS01-002
- 元レビュー: QF-RVR-MVS01-016
- 前回回答: QF-RVR-MVS01-017 `REVIEW_NOT_PERFORMED`
- Review mode: REVERIFY（Organizerが人手指定。Controller生成ではない）
- 固定implementation SHA: `4911801f1b1c51f6878e84d60e614dfaee9e8d25`
- 固定implementation tree: `f1d37e7a2965e1c795b343510c18bef54231ec2e`
- implementation parent: `a3f5ec7f1851fd1ed18836d3219c87ad7a315753`
- 元review対象: `c5d316063bc16161e2808c02334f331603b20c32`
- 状態: Draft / unmerged / `BOOTSTRAP_DISABLED`

## QF-RVR-MVS01-017のblock解消条件

本packetを含むPR #7 headがpushされた後、上記implementation SHAがrepository refから到達可能であることを`git fetch`と`git cat-file`で確認してください。packet/head commitやPR merge refをimplementation SHAの代わりに使用しないでください。

Organizer処置記録のcanonical pathは次です。

`docs/evidence/automation/dispositions/QF-ORG-MVS01-001-controller-r1-disposition.md`

これはOrganizer所有の記録です。Claudeの出力では全FindingのDispositionを`UNDECIDED`に維持し、Organizer記録との対応だけを検証してください。P3-015についてもClaude出力は`OPEN / UNDECIDED`、Organizer記録は`OPEN / DEFERRED`です。

## Independent reviewerへの依頼

QF-RVR-MVS01-016の15 Findingを上記固定commit/tree上で1件ずつ再検証してください。実装済みという申告だけでFindingを閉じず、独立証拠が揃わないものは`OPEN`を維持してください。

重点確認:

1. P0-001のdenylistがexact leading `./`だけを除去し、absolute、drive、backslash、NUL、空segment、`.`、`..`をfail-closedにすること。
2. P1-002のpatch identityが製品試験より前に封緘され、untrusted test workspaceから上書き不能であること。
3. P0/P1/P2のOrganizer `ACCEPTED_PLAN`と実装・workflow到達性が一致すること。
4. P3-011〜014の修正を実行または静的照合で確認すること。
5. P3-015はStep 2.5のlive実測証跡がない限り`OPEN`のままとすること。
6. 凍結仕様v0.5.1に差分がなく、新しいController runtime依存がないこと。
7. Draft、unmerged、`BOOTSTRAP_DISABLED`が維持されていること。

## 製造側の事前検証

固定implementation SHAの内容に対し、Controller acceptance 40/40、threat baseline preflight、actionlint、JSON検査、`git diff --check`、凍結仕様差分ゼロをローカルで確認した。90ケース回帰はGitHub trusted runnerの結果を正本とし、90/90 GREENになるまで`organizer:hold`とする。

## 期待するREVERIFY出力

- 対象commit/tree/parentの実測値
- 15 FindingそれぞれのVerification statusと根拠
- Claude dispositionが全件`UNDECIDED`であること
- Organizer処置記録との対応
- P3-015のStep 2.5待ち確認
- trusted runner上のController 40/40および90/90証跡
- Draft、unmerged、`BOOTSTRAP_DISABLED`維持の確認
