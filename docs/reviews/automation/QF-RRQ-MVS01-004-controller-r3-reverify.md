# Controller修正版R3 Claude REVERIFY依頼

- 文書ID: QF-RRQ-MVS01-004
- 元レビュー: QF-RVR-MVS01-016
- R1 REVERIFY: QF-RVR-MVS01-018
- R2 REVERIFY: QF-RVR-MVS01-019
- Review mode: REVERIFY（Organizerが人手指定。Controller生成ではない）
- 固定implementation SHA: `dcfc9e03cd82da07d9da3ad841fb13f9c9ed850d`
- 固定implementation tree: `ab04ccd8f4415ad4188917264cc20309dfbd04a9`
- implementation parent: `96b6482461b13d01c7da561c611601e9938a5c92`
- 状態: Draft / unmerged / `BOOTSTRAP_DISABLED`

## 対象

QF-RVR-MVS01-019が次SHAで要求した5件を固定implementation commit/tree上で
再検証してください。

1. `AUTO-IMPL-P1-018`: registry executionのevidence欠落判定がkey不在だけを
   欠落とし、`0`と空配列を有効な証跡として扱うこと。INITIAL/PASS/Finding 0件で
   `accepted=true` / `qf:review-green`、P2/P3のみの
   `PASS_WITH_FINDINGS`で`qf:changes-requested`となる正経路を受入試験が主張すること。
2. `AUTO-IMPL-P2-019`: `REV-GATE-017`と`loop_transition`が
   `verification_status=VERIFIED`のP0/P1をblocking対象から除外し、REVERIFYの
   VERIFIED済みP1で`blocking=false` / `PASS_WITH_FINDINGS`が受理されること。
3. `AUTO-IMPL-P2-020`: loop workflowが`gate.accepted is True`の場合だけ
   transitionを評価し、Gate拒否時は`qf:stopped`を付与すること。Controllerが
   non-zeroを返しても書き出した`loop-transition.json`を読み、STOP label経路へ
   到達できること。
4. `AUTO-IMPL-P3-021`: Organizer allowlist読取がappointment対象時だけ行われ、
   初回bootstrap例外が`TC-ACC-MVS01-094-BOOTSTRAP`としてowner / due / reason付きで
   登録され、Enablement gateが固定SHAへの独立人手署名を要求すること。
5. `AUTO-IMPL-P3-022`: 非凍結のController guide、`REVIEW.md`、navigation契約が
   `AUTO-T01`〜`AUTO-T39` / 40件で一致し、凍結v0.5.1の39件表記との差が
   non-frozen backlogとして記録されていること。v0.5.1自体は変更されていないこと。

`AUTO-IMPL-P3-015`はStep 2.5 live実測まで`OPEN`を維持してください。
Findingを`CLOSED`へ変更しないでください。

Organizer処置記録のcanonical path:

`docs/evidence/automation/dispositions/QF-ORG-MVS01-003-controller-r3-disposition.md`

Claude出力では22 FindingすべてのDispositionを`UNDECIDED`に維持し、Organizer記録との
対応だけを検証してください。

## trusted runner証拠

| 検査 | 結果 | Run / Job | Artifact / digest |
|---|---|---|---|
| Controller acceptance | 40/40 GREEN | `33224363223 / 99024928224` | `9706316045` / `sha256:34540741caec0a3f88dd4c2d18ef528ae07e309d253804f2ca6b802928cb751f` |
| Stage 6R-10 | 90/90 GREEN | `33224363249 / 99024928413` | `9706366619` / `sha256:f4838a56a5ed960179f6e7453a30b91bf7d2398f4d2b13c3c1f3ede6c559e8da` |
| Stage 6R-11 | 90/90 GREEN | `33224363204 / 99024928266` | `9706370074` / `sha256:dfbf5a5fe0834539d90f25709c2a373e02c4d419c68a9a832590bf3a9e3e72ff` |
| Repository navigation | GREEN | `33224363225 / 99024928389` | GitHub Actions log |

固定implementation tree内容に対してローカルでもController 40/40、test ID uniqueness、
navigation 56/56、taxonomy 47/47、actionlint、Python構文、JSON、
`git diff --check`、凍結仕様差分ゼロ、依存manifest差分ゼロを確認した。

role-appointment Run `33224363214` / Job `99024928168`はEXPECTED REDである。
PR #7がappointment recordを含み、Controllerがdefault branchへ未mergeのため、
trusted checkoutでOrganizer allowlistを取得できず証拠収集stepがfail-closed停止し、
Controller評価stepはskippedである。このREDを任命やPhase A有効化の証拠として使用しない。

## 期待するREVERIFY出力

- 対象commit/tree/parentのgit object実測
- 上記5件とP3-015のVerification statusおよび根拠
- 22 FindingのID単位再集計
- Claude dispositionが全件`UNDECIDED`であること
- Organizer処置記録との対応
- trusted runner証跡
- 凍結仕様・依存不変、Draft、unmerged、`BOOTSTRAP_DISABLED`維持の確認
