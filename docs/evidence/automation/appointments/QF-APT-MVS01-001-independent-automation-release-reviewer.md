# Independent Automation Release Reviewer appointment direction

- 文書ID: QF-APT-MVS01-001
- 種別: Organizer Appointment Direction / Bootstrap Signature Evidence
- 対象repository: `KojimaSusumu365/toi-no-mori-mvs01`
- 対象PR: Draft PR #7 `ops-github-autodrive-controller`
- Organizer: `KojimaSusumu365`
- Nominee: `SusumuKojima1967`（GitHub user ID `53677286`）
- 記録時刻: `2026-08-29T15:05:38Z`
- 状態: `RECORDED / PENDING ACTIVATION`

## Organizer instruction

Organizerは、`SusumuKojima1967`をIndependent Automation Release Reviewerとして
任命recordへ反映することを明示した。本記録はOrganizer側の人間署名を固定する。

## Independent bootstrap signature

| Evidence | Value |
|---|---|
| Signed PR comment | [issuecomment-5463053121](https://github.com/KojimaSusumu365/toi-no-mori-mvs01/pull/7#issuecomment-5463053121) |
| Comment author | `SusumuKojima1967` / user ID `53677286` |
| Comment created | `2026-08-29T14:46:59Z` |
| GitHub Review | [PRR_kwDOT_CKHM8AAAABLYEzVg](https://github.com/KojimaSusumu365/toi-no-mori-mvs01/pull/7#pullrequestreview-5058409302)（timeline ID `5058409302`） |
| Review author/state | `SusumuKojima1967` / `APPROVED` |
| Review submitted | `2026-08-29T14:57:31Z` |
| Review body | empty（署名内容は上記PR commentに記録） |
| Fixed implementation commit | `dcfc9e03cd82da07d9da3ad841fb13f9c9ed850d` |
| Fixed implementation tree | `ab04ccd8f4415ad4188917264cc20309dfbd04a9` |
| Technical review | QF-RVR-MVS01-020 — `PASS_WITH_FINDINGS / blocking=false` |
| Remaining risk | `AUTO-IMPL-P3-015` — `OPEN / DEFERRED` until Step 2.5 |

GitHub API上の署名comment authorとReview authorはともに`SusumuKojima1967`である。
comment本文の自己申告loginは`KojimaSusumu1967`と誤記されているため、本文文字列ではなく
GitHubが返すauthor login/user IDと同一accountの`APPROVED` Reviewをidentity根拠とする。
Review connectorはreview対象`commit_id`を返さないため、本記録は未取得値を推測しない。
署名commentが固定する対象は上記implementation commit/treeである。

## Independence checks

| Check | Result |
|---|---|
| Organizerと別GitHub account | PASS — `KojimaSusumu365` ≠ `SusumuKojima1967` |
| Repository permission | PASS — `read`（`write` / `maintain` / `admin`なし） |
| Organizer allowlist membership | PASS — `SusumuKojima1967`は非登録 |
| 残存リスクの明示 | PASS — P3-015 OPEN / DEFERREDを確認 |
| 禁止操作の維持 | PASS — merge、Draft解除、Phase A、secrets/App/rules、Stage 6R-12を非承認 |

## Activation boundary

本証拠は`TC-ACC-MVS01-094-BOOTSTRAP`の固定implementation SHAに対する独立人手署名と、
Organizerの任命反映指示を満たす。一方、凍結仕様v0.5.1 §3.4では、通常の任命PRは
role appointment recordだけを変更し、被任命者のcurrent-head `APPROVED`、決定論的
`qf-role-appointment-signature` GREEN、default branchへのmergeを必要とする。

PR #7はController実装・文書を含むmixed-change Draftであり、署名後の任命反映commitは
review時のheadより後になる。このためPR #7上のRequired Checkはfail-closed REDが正しく、
本記録だけで正式な`APPOINTED`またはPhase A enablementを宣言しない。権威recordは
`VACANT`のままnomineeとbootstrap evidenceを保持し、契約適合の有効化まで
`PENDING ACTIVATION`とする。

## State preserved

- PR #7: Draft / open / unmerged
- Controller: `BOOTSTRAP_DISABLED`
- frozen specification v0.5.1: byte-for-byte unchanged
- `AUTO-IMPL-P3-015`: `OPEN / DEFERRED`
- Step 2.5: not completed
- Stage 6R-12: not started
- secrets / GitHub App / rules: not configured by this record
