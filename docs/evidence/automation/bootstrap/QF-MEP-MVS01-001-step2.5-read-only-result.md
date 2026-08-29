# Step 2.5 read-only live measurement result

- 文書ID: `QF-MEP-MVS01-001-MR1`
- 根拠パケット: `QF-MEP-MVS01-001`
- 対象: `KojimaSusumu365/toi-no-mori-mvs01` / Draft PR #7・#8
- captured_at: `2026-08-29T15:49:09.033Z`
- captured_by: GitHub connector authenticated as `KojimaSusumu365`
- 実行境界: read-only。secret、GitHub App、ruleset、branch protection、Draft、PR state、branch refは変更していない
- 総合判定: `PARTIAL / NOT AUTHORIZED TO ENABLE`
- `AUTO-IMPL-P3-015`: `OPEN / DEFERRED`を維持
- `TC-ACC-MVS01-092-STEP`: 台帳の`not-run`を維持

## 結論

PR #8では、appointment対象変更について`pull_request/opened`、
`pull_request/synchronize`、`pull_request_review/submitted`に対応する3つのlive runを確認した。
Independent Reviewerの`APPROVED` Reviewはcurrent head
`e337c0c212432923cb4e64b788156f9ea8daa163`へ固定されている。

ただし、3 runとも`main`をtrusted checkoutした後、
`.github/ai/registries/organizer-allowlist.yml`が`main`に存在しないため
Gather stepでfail-closedとなり、決定論的Controller評価はskippedとなった。
さらに`main`はbranch protection無効、ruleset 0、Required Check 0で、Controller workflowも
default branchに存在しない。このためcurrent-head approvalによるGREEN、dismiss後のRED、
non-appointment applicabilityをlive実証できず、P3-015の完了条件は満たさない。

## 固定identityと状態保存

| 項目 | 実測値 | 判定 |
|---|---|---|
| fixed implementation | `dcfc9e03cd82da07d9da3ad841fb13f9c9ed850d` | 固定 |
| implementation tree | `ab04ccd8f4415ad4188917264cc20309dfbd04a9` | 固定 |
| PR #7 | base `b6959e86…`, head `de0215c7…`, Draft / OPEN / unmerged | PASS |
| PR #8 | base `de0215c7…`, head `e337c0c2…`, Draft / OPEN / unmerged | PASS |
| PR #7 appointment record | `VACANT`, nominee `SusumuKojima1967` | 維持 |
| PR #8 candidate record | `APPOINTED`候補。ただしcurrent-head GREENとdefault mergeまで非発効 | PENDING ACTIVATION |
| reviewer permission | `read` | PASS |
| Organizer allowlist | `KojimaSusumu365`のみ。Reviewerは非登録 | PASS |
| controller phase | branch上のbaselineは`BOOTSTRAP_DISABLED`。repository variable実値は未取得 | PARTIAL |

PR #7 Review `5058409302`はcommit `74ce0021…`への承認であり、現head
`de0215c7…`へのcurrent-head承認ではない。PR #8 Review `5058483549`はcurrent head
`e337c0c2…`への`APPROVED`である。

## Track B — event coverage

| Event / activity | live evidence | 結果 |
|---|---|---|
| `pull_request/opened` | PR #8 initial head `c926b447…`; Run [33259955631](https://github.com/KojimaSusumu365/toi-no-mori-mvs01/actions/runs/33259955631), Check Run `99120075898`, Suite `90138389774` | OBSERVED / RED |
| `pull_request/synchronize` | PR #8 current head `e337c0c2…`; Run [33259973672](https://github.com/KojimaSusumu365/toi-no-mori-mvs01/actions/runs/33259973672), attempt 1 Job `99120122749`, attempt 2 Check Run `99121825173` | OBSERVED / RED |
| `pull_request_review/submitted` | Reviewer `SusumuKojima1967`; Review `5058483549`; Run [33260573240](https://github.com/KojimaSusumu365/toi-no-mori-mvs01/actions/runs/33260573240), Check Run `99121689668`, Suite `90139999077` | OBSERVED / RED |
| `pull_request/reopened` | state変更を伴うため実行せず | NOT_EXECUTED |
| `pull_request/ready_for_review` | Draft解除禁止のため実行せず | NOT_EXECUTED |
| `pull_request_review/edited` | Review変更を伴うため実行せず | NOT_EXECUTED |
| `pull_request_review/dismissed` | Review取消を伴うため実行せず | NOT_EXECUTED |
| appointment applicability | PR #8 changed fileはappointment record 1件だけ | INPUT OBSERVED / EVALUATOR NOT REACHED |
| non-appointment applicability | 対象PR内で副作用なしに新activityを発生できない | NOT_MEASURED |
| current-head APPROVED activation | Reviewのcommit bindingはPASS、Required Checkはfailure | FAIL |
| dismissal後RED transition | dismissal禁止のため未実施 | NOT_MEASURED |

Activity名はrun payloadに直接格納されないため、`opened`と`synchronize`はPR #8のcommit
sequenceとrun作成時刻、`submitted`はReview提出時刻・triggering actor・
`event=pull_request_review`から対応付けた。これは証拠JSONで`inferred_activity=true`としている。

3 runのfailure原因は同じである。workflowは`ref: main`をcheckoutし、Gather stepが
trusted Organizer registryを読むが、`main`の当該pathは404である。Action logには
`ENOENT: no such file or directory, open '.github/ai/registries/organizer-allowlist.yml'`
が記録され、後続`Evaluate appointment in the deterministic Controller`はskippedとなった。

## PR current-head Checksと回帰artifact

PR #7 headではCheck Run 9件中8件success、`qf-role-appointment-signature`のみfailure。
PR #8 headではCheck Run 10件中8件success、同role checkが2件failure
（synchronize rerunとreview submitted）。

| PR | Gate | Run / Job | 結論 | artifact |
|---|---|---|---|---|
| #7 | Controller 40/40 | `33259552412` / `99119029619` | success | `9716843695`, `sha256:fb8eb933…`, 30日 |
| #7 | Stage 6R-10 90/90 | `33259552386` / `99119029627` | success | `9716884404`, `sha256:15203184…`, 30日 |
| #7 | Stage 6R-11 90/90 | `33259552390` / `99119029539` | success | `9716884099`, `sha256:ad54c5f5…`, 30日 |
| #8 | Controller 40/40 | `33259973633` / `99120122732` | success | `9716961265`, `sha256:331af8eb…`, 30日 |
| #8 | Stage 6R-10 90/90 | `33259973625` / `99120159920` | success | `9717007874`, `sha256:4a00282a…`, 30日 |
| #8 | Stage 6R-11 90/90 | `33259973622` / `99120155796` | success | `9717009575`, `sha256:91a2da2c…`, 30日 |

全artifactは取得時点で`expired=false`。created/expiresの差から約30日のeffective retentionを
実測した。AI transportで静的に指定された7日retentionとAction log retentionはlive runが
無いため未測定である。

## Track C — repository / permission / transport

| 測定項目 | 観測値 | 判定 |
|---|---|---|
| repository visibility | `public` | PASS |
| authenticated actor権限 | admin/maintain/push/pull/triage=true | OBSERVED |
| fork PR Actions approval | settings APIはconnector非対応、browserは未ログイン | NOT_MEASURED |
| default `GITHUB_TOKEN` permission | 同上 | NOT_MEASURED |
| ActionsによるPR approval可否 | 同上 | NOT_MEASURED |
| `main` branch protection | `protected=false`; enforcement `off` | FAIL |
| Required Checks | contexts/checksとも空 | FAIL |
| repository rulesets | `[]` | OBSERVED — bypass actorなし |
| default branch workflows | `stage6r4c-nonroot-postgresql.yml` 1件のみ | FAIL — Controller未配置 |
| Actions費用条件 | billing/settings endpoint未取得 | NOT_MEASURED |
| artifact retention | 実在6 artifactは約30日 | PASS/PARTIAL |
| log retention | retention setting未取得 | NOT_MEASURED |
| Job B `contents: read` checkout | workflow静的宣言あり、Phase A live jobなし | NOT_MEASURED |
| Claude auth | custom API-key profile・`id-token`宣言なしを静的確認、live jobなし | NOT_MEASURED |
| QF GitHub App install/permissions | repository installation/settings endpoint未取得。connector App installationとは区別 | NOT_MEASURED |
| App bypass | ruleset自体は0。特定QF App設定は未取得 | NOT_MEASURED |
| Work Order `workflow_sha` | `${{ github.workflow_sha }}`を静的確認、push live runなし | NOT_MEASURED |
| PR label最小権限 | `pull-requests: write` / `issues`なしを静的確認、smokeは変更禁止 | NOT_MEASURED |
| Review Result durability | `main`にpathなし、PR #7は`.gitkeep`のみ、publisher live runなし | FAIL/NOT_MEASURED |
| Registry alignment | PR #7 API blob SHAとlocal git blobが7/7一致、40/40 Check GREEN | PASS |
| Disposition transport | defaultにcontent-addressed recordなし、live publisherなし | NOT_MEASURED |

設定API `actions/permissions`、`actions/permissions/workflow`、
`actions/permissions/fork-pr-contributor-approval`、`installation`は利用したGitHub read
connectorの許可endpoint外で400となった。branch protection詳細endpointはintegrationから
403だったが、branch endpointは`protected=false`と`required_status_checks.enforcement_level=off`
を返した。settings画面はin-app browserの未ログインsessionでは404であったため、これらを
推測値で埋めていない。

## P3-015をCLOSEできないgap

1. `reopened`、`ready_for_review`、review `edited`、`dismissed`のlive activityが未観測。
2. non-appointment変更の`not_applicable`結果が未観測。
3. current-head approvalは存在するが、trusted default branch欠落によりController evaluatorへ到達せずGREENにならない。
4. dismissal後のRED遷移を再現していない。
5. `main`でRequired Checkを強制するbranch protection/rulesetが存在しない。
6. append-onlyなcontent-addressed measurement/review/disposition recordのlive publicationが未観測。
7. Actions settings、QF GitHub App、Claude/Job B/label/Work Orderのlive permission測定が残る。

したがって本結果はStep 2.5の「開始・部分実測」証拠であり、完了証拠ではない。
PR #7/#8のDraft・OPEN、`VACANT / PENDING ACTIVATION`、`BOOTSTRAP_DISABLED`、
P3-015 OPEN/DEFERRED、Stage 6R-12 NOT STARTEDを維持する。

## 主要evidence URL

- [Repository](https://github.com/KojimaSusumu365/toi-no-mori-mvs01)
- [PR #7](https://github.com/KojimaSusumu365/toi-no-mori-mvs01/pull/7)
- [PR #8](https://github.com/KojimaSusumu365/toi-no-mori-mvs01/pull/8)
- [PR #8 current-head approval](https://github.com/KojimaSusumu365/toi-no-mori-mvs01/pull/8#pullrequestreview-5058483549)
- [PR #8 review-submitted role check](https://github.com/KojimaSusumu365/toi-no-mori-mvs01/actions/runs/33260573240/job/99121689668)
- [PR #8 synchronize role check](https://github.com/KojimaSusumu365/toi-no-mori-mvs01/actions/runs/33259973672/job/99121825173)
- [PR #7 Controller 40/40](https://github.com/KojimaSusumu365/toi-no-mori-mvs01/actions/runs/33259552412)
- [PR #8 Controller 40/40](https://github.com/KojimaSusumu365/toi-no-mori-mvs01/actions/runs/33259973633)

型付き完全レコードは`QF-MEP-MVS01-001-step2.5-read-only-evidence.json`を参照する。
