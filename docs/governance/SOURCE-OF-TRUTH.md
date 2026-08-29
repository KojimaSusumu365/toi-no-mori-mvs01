# Source of truth and identity ledger

## 正本の優先順位

1. Git objectで固定された実装・test
2. そのobjectを実行したGitHub ActionsのRun / attempt / Job / artifact
3. Stage manifestとevidence文書
4. review文書
5. 会話要約

会話に書かれたSHAや件数がGitHubと衝突する場合、GitHubの型付き識別子を採用し、不一致をFindingとして残します。

## Stage 6R-11R review target

| 型 | 値 | 用途 |
|---|---|---|
| implementation commit | `61b55e03d1c3df7355eb3cf15aa1f1fcad7870e1` | Claudeがreviewする固定実装HEAD |
| implementation tree | `23de94ef1e6ded9e2122b11880b7cb80ff8378ae` | 上記commitが参照するtree object |
| base commit | `60c10feb1fed4b4b5000fac4145aa4def591877f` | stacked PR #4のbase |
| evaluated PR merge ref | `83857ee48d4f5317dddf0023a8821a67e3e62980` | workflowがcheckoutしたbase+headの仮想merge |
| relationship | `pull_request_merge_ref` | headがmerge refのancestorであることをworkflowが検証 |
| workflow run | `33135504039` attempt 1 | Stage 6R-11実行 |
| job | `98734412669` | 90/90 gate job |
| artifact | `9671907000` | immutable evidence artifact |
| artifact digest | `sha256:fd4bd48943a0d9c6fb4f3fb20622856503f2f2783070da2070f3cd85878a1955` | artifact integrity |

`23de94ef1e6ded9e2122b11880b7cb80ff8378ae` はtree objectです。branch HEAD commitと記載してはいけません。  
`83857ee48d4f5317dddf0023a8821a67e3e62980` はPR merge refです。implementation HEADと記載してはいけません。

Review packetをsealする後続documentation commitは、target実装を変更しません。Claudeのreview対象は常に `61b55e03d1c3df7355eb3cf15aa1f1fcad7870e1` です。

## Taxonomy overlay evidence

| Type | Value |
|---|---|
| overlay commit | `80090e2eb56c4ddf438867572f8f6e8c389813ba` |
| overlay tree | `5829359435e7d07a17196182653cbc72ae93e641` |
| base / parent | `b85459ff1db304346e159e75833b1c415ce7a575` |
| Stage 6R-10 Run / Job | `33139913725 / 98748216295` — success, historical 85/85 writer |
| Stage 6R-10 artifact | `9673573611`, `sha256:02922ab03a11eec9dab41141fff03dc7b996f53542b9762a3a0d7330f61ee155` |
| navigation/taxonomy Run / Job | `33139913729 / 98748216209` — success |
| Stage 6R-11 Run / Job | `33139913757 / 98748216596` — success, 90/90 |
| Stage 6R-11 artifact | `9673576028`, `sha256:16c7a9e3f6b52674eaec601a5ac70f41a173c7af59ab01743ff85f6dcccc3ea8` |

The GitHub API was rechecked on 2026-08-28. Each artifact identifies
`stage-gh-org-1-physical-taxonomy@80090e2` as its head. The navigation job has no
artifact by design; its Run, Job, head commit and successful steps are the identity.

## Final Closure response evidence

| Type | Value |
|---|---|
| response commit | `497d786fe687069c004b89b86b2b9345faeb9726` |
| response tree | `ba3711b6597013df8b268dc764098e7ed68681e6` |
| evaluated PR #6 merge ref | `51e02a0488fbfdfaef3e26c05cc421e999e6d41d` |
| merge-ref parents | `80090e2eb56c4ddf438867572f8f6e8c389813ba`, `497d786fe687069c004b89b86b2b9345faeb9726` |
| navigation Run / Job | `33152117524 / 98786286113` — success |
| Stage 6R-10 Run / Job | `33152117623 / 98786286856` — 90/90 success |
| Stage 6R-10 artifact | `9678180236`, `sha256:44a0d252b572123c68afc43d4f7cad85083d0951815fa9638066f483d80a6261` |
| Stage 6R-11 Run / Job | `33152117552 / 98786286664` — 90/90 success |
| Stage 6R-11 artifact | `9678188675`, `sha256:3a04014251c64cf3ee5c69660c21697cdce45fd8848a08bfa95b44d477fd0b1e` |

The Stage 6R-11 artifact records the response tree, verified inclusion of the
authoritative head and both merge-ref parents. This relationship remains
reconstructable after GitHub recalculates the live PR merge ref.

## Baseline chain

- `main@c90dfdb154d99ee480571c8a397e99d88e12dea8` remains unchanged.
- PR #1 is the cumulative Stage 6R-1〜11 baseline.
- PR #3 adds repository navigation on top of PR #1.
- PR #4 adds Stage 6R-11R on top of PR #3.
- PR #5 adds the physical taxonomy on top of PR #4.
- PR #6 adds final review responses and manufacturing evidence on top of PR #5.
- PR #7 adds the disabled GitHub auto-drive Controller on top of PR #6.
- No PR is merged and all remain Draft.

## GitHub auto-drive Controller identity

| Type | Value |
|---|---|
| Draft PR | `#7` |
| implementation commit | `dcfc9e03cd82da07d9da3ad841fb13f9c9ed850d` |
| implementation tree | `ab04ccd8f4415ad4188917264cc20309dfbd04a9` |
| superseded implementation | `a673dded7edc5d851fd0ce16ccfc025a86ae6475` — QF-RVR-MVS01-019 review target |
| implementation parent | `96b6482461b13d01c7da561c611601e9938a5c92` |
| stacked base | `b6959e86713c89b37a8d0e8009f402512c02e346` |
| branch | `ops-github-autodrive-controller` |
| state | `BOOTSTRAP_DISABLED` |
| review packet | `docs/reviews/automation/QF-RRQ-MVS01-004-controller-r3-reverify.md` |
| independent REVERIFY | `docs/reviews/automation/QF-RVR-MVS01-020-controller-r3-reverify.md` — `PASS_WITH_FINDINGS`, 21 VERIFIED / 1 OPEN |
| Organizer disposition | `docs/evidence/automation/dispositions/QF-ORG-MVS01-004-controller-r3-final-disposition.md` — 21 CLOSED_VERIFIED / 1 OPEN_DEFERRED |
| next measurement packet | `docs/reviews/automation/QF-MEP-MVS01-001-controller-step2.5-measurement.md` |

The review and disposition packets are later documentation-only commits. Claude
reviewed the fixed implementation commit/tree above, not a packet commit or a
recalculated PR merge ref. QF-RVR-MVS01-020 independently verified 21 Findings;
the Organizer closed those 21 as `CLOSED_VERIFIED`. P3-015 remains the sole
`OPEN / DEFERRED` Finding until Step 2.5 live event coverage is measured. This
Finding disposition is not final Controller acceptance and does not authorize
Draft removal, merge or Phase A.

## Deferred-test source of truth

`spec/deferred-tests.json` is the machine-readable source of truth for every
current not-run test, including owner, reason and due condition. Review manifests
and evidence tables must copy those values exactly and may not define alternatives.

## 必須台帳項目

各受入evidenceは次を同じレコードに保存します。

- stage
- repository
- branch
- head commit
- base commit
- PR merge ref commit（PR event時）
- workflow name / workflow id
- run id / attempt
- job id / gate name
- expected / passed / failed / not-run
- artifact name / digest / retention
- headと実評価対象のrelationship
- captured_at

## relationship値

- `same_commit`: checkout対象が記録commitと同じ
- `pull_request_merge_ref`: baseとheadの仮想mergeを評価
- `different_verified_commit`: 別commitだが関係を証明済み
- `unknown`: 証明できない。受入根拠にしない

PR eventではAPIの `head_sha` だけからcheckout対象を断定しません。merge ref、workflow checkout設定、ancestor検査を同じ証跡に残します。
