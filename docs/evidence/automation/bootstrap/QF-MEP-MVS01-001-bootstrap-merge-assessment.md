# Initial bootstrap merge readiness assessment

- 文書ID: `QF-MEP-MVS01-001-BMA1`
- 根拠: `QF-MEP-MVS01-001-MR1`の`PARTIAL`受領
- repository: `KojimaSusumu365/toi-no-mori-mvs01`
- 対象: PR #1、#3、#4、#5、#6、#7
- captured_at: `2026-08-29T15:59:48.006Z`
- captured_by: GitHub connector authenticated as `KojimaSusumu365`
- 実行境界: read-only。merge、Draft解除、PR retarget、review、label、branch、設定は変更していない
- 現時点の判定: **`NO_GO_NOW / CONDITIONAL_BOOTSTRAP_CANDIDATE`**

## Organizer向け結論

先行PR chainはlive GitHub上で完全に連結し、PR #1・#3〜#6のcurrent-head Checkは
すべてGREENである。各PRは`mergeable=true / mergeable_state=clean`であり、code-levelの
衝突は観測されなかった。PR #7も`mergeable=true`だが、bootstrap role check 1件がREDのため
`mergeable_state=unstable`である。

ただし、現在の状態から直ちにmergeすることはできない。全PRがDraftであり、PR #3以降は
前PRのbranchをbaseにしたstacked PRなので、mainへ順次反映するには各先行merge後の
base retargetとfresh Checkが必要である。加えて、PR #7はStep 2.5が`PARTIAL`、
P3-015が`OPEN / DEFERRED`、Reviewerが`VACANT / PENDING ACTIVATION`、current-head role checkが
REDであり、外部settingsの一部は`NOT_MEASURED`である。

したがって本資料は「技術的にbootstrap merge候補へ進めるchainである」ことを確認するが、
Draft解除またはmergeのGO判定は出さない。GOへ進むには、後述する限定bootstrap判断と
settingsのread-only確認、current merge targetへの署名、順序付きfresh Checkが必要である。

## Live PR chain

| PR | Base SHA → Head SHA | Draft / state | mergeability | current-head Checks | GitHub Review |
|---|---|---|---|---|---|
| [#1](https://github.com/KojimaSusumu365/toi-no-mori-mvs01/pull/1) | `main@c90dfdb1…` → `4537085c…` | Draft / OPEN | clean | 2/2 success | 0 |
| [#3](https://github.com/KojimaSusumu365/toi-no-mori-mvs01/pull/3) | `4537085c…` → `60c10feb…` | Draft / OPEN | clean | 3/3 success | 0 |
| [#4](https://github.com/KojimaSusumu365/toi-no-mori-mvs01/pull/4) | `60c10feb…` → `b85459ff…` | Draft / OPEN | clean | 3/3 success | 0 |
| [#5](https://github.com/KojimaSusumu365/toi-no-mori-mvs01/pull/5) | `b85459ff…` → `80090e2e…` | Draft / OPEN | clean | 3/3 success | 0 |
| [#6](https://github.com/KojimaSusumu365/toi-no-mori-mvs01/pull/6) | `80090e2e…` → `b6959e86…` | Draft / OPEN | clean | 3/3 success | 0 |
| [#7](https://github.com/KojimaSusumu365/toi-no-mori-mvs01/pull/7) | `b6959e86…` → `de0215c7…` | Draft / OPEN | unstable | 8 success / 1 failure | 1 APPROVED on `74ce0021…`, not current head |

連結は次の完全一致で確認した。

```text
main@c90dfdb154d99ee480571c8a397e99d88e12dea8
  -> PR #1 head 4537085c25ed3178214b0693afac7e42ce1b64de
  == PR #3 base
  -> PR #3 head 60c10feb1fed4b4b5000fac4145aa4def591877f
  == PR #4 base
  -> PR #4 head b85459ff1db304346e159e75833b1c415ce7a575
  == PR #5 base
  -> PR #5 head 80090e2eb56c4ddf438867572f8f6e8c389813ba
  == PR #6 base
  -> PR #6 head b6959e86713c89b37a8d0e8009f402512c02e346
  == PR #7 base
  -> PR #7 head de0215c74bc02b40883267af5f7dd7c1d8a763b6
```

凍結v0.5.1のStep 2は当時存在した`#1 → #3 → #4 → #5`を記録する。その後の権威ledgerは、
Stage 6R-11R final closureの#6とController #7を追加している。今回のOrganizer指示に従い、
bootstrap前提chainは`#1 → #3 → #4 → #5 → #6 → #7`として評価した。

## PR #7 identityとbootstrap例外

| 項目 | 観測値 | 判定 |
|---|---|---|
| fixed implementation | `dcfc9e03cd82da07d9da3ad841fb13f9c9ed850d` | 固定 |
| fixed tree | `ab04ccd8f4415ad4188917264cc20309dfbd04a9` | 固定 |
| current PR head | `de0215c74bc02b40883267af5f7dd7c1d8a763b6` | fixed implementationより3 commits後 |
| frozen v0.5.1 blob | fixed/currentとも`537de88c1e67a7b5e534c2834665ced10e898fe8` | 不変 |
| Controller 40/40 | Check Run `99119029619` success | PASS |
| Stage 6R-10 / 6R-11 | `99119029627` / `99119029539` success、各90/90 | PASS |
| role check | Check Run `99119029644` failure | EXPECTED BOOTSTRAP RED |
| bootstrap signature | fixed implementation/treeへの独立署名あり | RECORDED |
| GitHub Review | Review `5058409302`は`74ce0021…`へのAPPROVED | current-headではない |
| `74ce0021…`→current head | 1 documentation/governance commit、7 files、実装/workflow/schema変更なし | bounded delta |

role checkのREDはMR1で確認したとおり、trusted `main`にControllerとOrganizer allowlistが
まだ存在しないことによるfail-closedである。`TC-ACC-MVS01-094-BOOTSTRAP`の人手署名は
固定implementationを確認するための初回bootstrap証拠だが、それ自体はStep 2.5完了、
Reviewer activation、current-head GREENまたはmerge許可を意味しない。

## Default branchと外部設定

| 項目 | live観測 | 判定 |
|---|---|---|
| repository visibility | `public` | PASS |
| `main` | `c90dfdb154d99ee480571c8a397e99d88e12dea8` | unchanged |
| branch protection | `protected=false`; required status enforcement `off` | NO ENFORCEMENT |
| rulesets | `[]` | 未設定をlive確認 |
| default branch workflow | `stage6r4c-nonroot-postgresql.yml` 1件のみ | Controller未配置 |
| candidate baseline phase | `BOOTSTRAP_DISABLED` | static/live branch content確認 |
| role appointment | `VACANT`, nominee `SusumuKojima1967` | PENDING ACTIVATION |
| `QF_AI_PHASE` repository variable | connectorから取得不能 | NOT_MEASURED |
| Actions default token / approval / fork設定 | connectorから取得不能 | NOT_MEASURED |
| QF secret namesの有無 | connectorから取得不能 | NOT_MEASURED |
| QF GitHub App installation/permissions | connectorから取得不能 | NOT_MEASURED |

本作業は設定へ一切writeしていないため、既存状態を変更していない。ただしsecret、App、
repository variableは「変更していない」ことと「liveで未設定を証明した」ことを区別する。
PR #7をmainへ入れる前にRepository Ownerのsigned-in read-only設定証拠が必要である。

最低限、次の不存在または無効状態を確認する。

- Repository variable `QF_AI_PHASE`が不存在またはunsetであり、`A`ではないこと
- Actions secrets `OPENAI_API_KEY`、`ANTHROPIC_API_KEY`、
  `QF_GITHUB_APP_ID`、`QF_GITHUB_APP_PRIVATE_KEY`が未登録であること
- QF publisher用GitHub Appが対象repositoryへ未installであること
- Appがbranch protection/ruleset bypass actorでないこと
- Actionsのdefault token、PR approval、fork approval設定の実測値

## 現時点のNO-GO理由

1. PR #1・#3〜#7はすべてDraftであり、今回の指示ではDraft解除が禁止されている。
2. PR #3以降のbaseは`main`ではない。mainへ順次反映するには先行merge後のretargetが必要だが、今回は禁止されている。
3. PR #7のcurrent-head Required CheckはGREENではなく、branch protectionもRequired Checkを強制していない。
4. PR #7のGitHub Reviewはcurrent headではない。固定implementation署名はあるが、merge target全体へのcurrent-head署名は未取得である。
5. `QF-MEP-MVS01-001-MR1`は`PARTIAL`で、P3-015と`TC-ACC-MVS01-092-STEP`は未完了である。
6. Reviewerは`VACANT / PENDING ACTIVATION`である。
7. `QF_AI_PHASE`、secret名、QF App installationのlive不存在を現在のconnectorでは確認できない。
8. MR1と本評価は現時点でローカル成果物であり、repository上のdurable evidenceには未掲載である。

## 条件付きbootstrap GOへ移すための前提

次をすべて満たした後にのみ、Organizerは限定bootstrap GOを検討できる。

1. Repository Ownerが上記settingsをsigned-in read-onlyで確認し、時刻付き証拠を固定する。
2. Organizerが、P3-015をPhase A前まで`OPEN / DEFERRED`に維持したままController control-planeだけを先行配置する「一回限りのbootstrap sequencing判断」を明示する。
3. その判断はPhase A、secrets/App/rules設定、Stage 6R-12を許可しないことを明記する。
4. MR1、本評価、外部settings証拠をdurable governance recordとして公開する。
5. Independent ReviewerとOrganizerが、実際にmergeするcurrent head SHA/treeとfixed implementation ancestryを再確認する。
6. PR #1から一件ずつ、先行PRをmainへmergeした後に次PRをmainへretargetし、diff、head SHA、mergeability、fresh Checksを再取得する。
7. accepted commit SHAをmain historyに保持するため、原則としてmerge commitを用いる。squash/rebaseを選ぶ場合は新SHAを再固定・再検証する。
8. PR #7直前に`BOOTSTRAP_DISABLED`、`VACANT`、P3-015 OPEN、secret/App/rules未設定をもう一度確認する。

## 将来の実行シーケンス案（本作業では未実行）

```text
Owner settings attestation
  -> Organizer bounded-bootstrap decision
  -> PR #1: ready / fresh checks / merge to main
  -> PR #3: retarget main / fresh checks / merge
  -> PR #4: retarget main / fresh checks / merge
  -> PR #5: retarget main / fresh checks / merge
  -> PR #6: retarget main / fresh checks / merge
  -> PR #7: retarget main / current-head signatures / fresh checks
  -> Organizer final bootstrap acceptance
  -> manual PR #7 merge with BOOTSTRAP_DISABLED
  -> PR #8 appointment activation and remaining Step 2.5
  -> P3-015 close candidate
  -> separate authorization for secrets/App/rules and Phase A
```

各矢印は独立した変更操作であり、後続操作を自動承認しない。途中でhead drift、Check RED、
unexpected workflow、secret/App/rule差分が出た場合は即`organizer:hold`へ戻す。

## State preservation

- PR #1・#3〜#7: Draft / OPEN / unmerged
- PR #8: 変更なし
- `main`: 更新なし
- PR base/head: retargetなし、ref更新なし
- Controller: `BOOTSTRAP_DISABLED`
- Reviewer: `VACANT / PENDING ACTIVATION`
- P3-015: `OPEN / DEFERRED`
- secrets / GitHub App / rules / branch protection: 変更なし
- Phase A: 禁止・未開始
- Stage 6R-12: `NOT STARTED`

型付きlive snapshotは`QF-MEP-MVS01-001-bootstrap-merge-evidence.json`へ保存した。
