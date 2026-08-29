# Controller修正版R2 Claude REVERIFY依頼

- 文書ID: QF-RRQ-MVS01-003
- 元レビュー: QF-RVR-MVS01-016
- R1 REVERIFY: QF-RVR-MVS01-018
- Review mode: REVERIFY（Organizerが人手指定。Controller生成ではない）
- 固定implementation SHA: `a673dded7edc5d851fd0ce16ccfc025a86ae6475`
- 固定implementation tree: `4bb4ee8a0db5025ca06c96f45cbd27f8c54a1015`
- implementation parent: `76c52299d0367564e2212cf00e7baca5ee4c7434`
- 状態: Draft / unmerged / `BOOTSTRAP_DISABLED`

## 対象

QF-RVR-MVS01-018が次SHAで要求した4件を固定implementation commit/tree上で再検証してください。

1. `AUTO-IMPL-P1-005`: publisherがController `review-record`を呼び、inline hash実装を除去したこと。Disposition経路が`TC-ACC-MVS01-093-DISPOSITION`としてowner / due / reason付きで明示繰延されていること。
2. `AUTO-IMPL-P2-009`: runtime canaryがCodex prompt fileとClaudeの明示的なread directoryへ入り、両Agentが読める入力に対してoutput検査が行われること。
3. `AUTO-IMPL-P2-016`: role-appointment Required Checkが`${QF_AI_PHASE:-BOOTSTRAP_DISABLED}`を`preflight --expected-phase`へ渡し、repository variableとbaseline fileの一致強制を両phaseで維持すること。
4. `AUTO-IMPL-P3-017`: AUTO-T01がcode定数やregistryから導出しない直書き期待集合を持つこと。

`AUTO-IMPL-P3-015`はStep 2.5 live実測まで`OPEN`を維持してください。R1で`VERIFIED`とされたFindingを`CLOSED`へ変更しないでください。

## 018集計の照合依頼

QF-RVR-MVS01-018の個別表と各Finding節は既存15件を`VERIFIED 12 / OPEN 3`と記録しています。新規2件を加えると、R1時点のOPEN IDは`P1-005`、`P2-009`、`P3-015`、`P2-016`、`P3-017`の5件です。§6の`VERIFIED 11 / OPEN 4`、`Open total 6`との差をID単位で再集計し、R2出力で訂正または根拠を明示してください。

Organizer処置記録のcanonical path:

`docs/evidence/automation/dispositions/QF-ORG-MVS01-002-controller-r2-disposition.md`

Claude出力では全FindingのDispositionを`UNDECIDED`に維持し、Organizer記録との対応だけを検証してください。

## 製造側の事前検証

固定tree内容に対し、Controller acceptance 40/40、CLI `review-record` candidate生成、navigation 56/56、actionlint、JSON、Python構文、`git diff --check`、凍結仕様差分ゼロ、依存manifest差分ゼロをローカルで確認した。GitHub trusted runnerのController 40/40およびStage 6R-10 / 6R-11 90/90はpacket push後のRunを正本とする。

## 期待するREVERIFY出力

- 対象commit/tree/parentの実測値
- 上記4件とP3-015のVerification statusおよび根拠
- 17 FindingのID単位の再集計
- Claude dispositionが全件`UNDECIDED`であること
- Organizer処置記録との対応
- trusted runner証跡
- 凍結仕様・依存不変、Draft、unmerged、`BOOTSTRAP_DISABLED`維持の確認
