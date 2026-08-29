# Stage 6R-11R 応答 commit の再検証

- 文書ID: QF-RVR-MVS01-015
- 版: Version 0.1
- 日付: 2026-08-28
- 対象: 応答 commit `497d786fe687069c004b89b86b2b9345faeb9726`
- 応答主体: 外部レビュー側（Claude）
- 種別: **RVR-N17〜N22 の reviewer 側再検証。** Stage の Closure 判定は Organizer が既に記録済みであり、本書はそれを変更しない
- Repository への書込み: 無し

---

## 0. 結論

**RVR-N17〜N22 の 6 件すべてを、応答 commit `497d786` の git object から独立に確認した。**

`closure.md` §E と `final-acceptance.md` は、この 6 件について次のとおり正しく記していた。

> RVR-N17 through RVR-N22: `ACCEPTED`; implemented and verified by the affected
> GitHub manufacturing gates. **They are not mislabelled as a second Claude
> re-verification.**

**その但し書きは、本書をもって不要になる。** 6 件は reviewer 側で `VERIFIED` である。

---

## 1. 私に帰属する記録の確認

`docs/reviews/stage6r11r/claude-findings.md` は QF-RVR-MVS01-007 を repository 様式へ正規化したものである。**私に帰属する記録であるため、内容が私の述べたことと一致するかを確認した。**

| 項目 | 判定 |
|---|---|
| Overall decision（`PASS_WITH_FINDINGS`、blocking なし） | 一致 |
| RVR-N10〜N16 の `CLOSED_VERIFIED` と各理由 | 一致 |
| RVR-N15 の「artifact self-description は N18 へ移送」 | 一致 |
| 「reviewer 側決定の最終確定は owner acceptance による」 | 一致 |
| RVR-N17〜N22 の内容と required response | 一致 |
| Review limitations（Actions API へ到達できず、Run/Job/artifact 識別子は packet からの引用） | **一致。かつ重要な限定が省略されていない** |
| Stage 6R-12 を「手続き未了ゆえ未承認」とした理由付け | 一致 |

**誇張も省略もない。** とくに Review limitations 節が原文の限定をそのまま残し、そのうえで「Codex が後に GitHub API で PR #5 の Run・Job・conclusion・head SHA・artifact digest を検証した」と補ったのは正確な扱いである。**私が確認していないことを、私が確認したことにしていない。**

---

## 2. Closure identity の照合

git object から独立に確認した。

```
応答 commit 497d786 の tree      = ba3711b6597013df8b268dc764098e7ed68681e6   ← 記載と一致
応答 commit 497d786 の parent    = 80090e2eb56c4ddf438867572f8f6e8c389813ba   ← PR #5 head
PR #6 merge ref 51e02a0 parents  = 80090e2（base）, 497d786（head）           ← 記載と一致
497d786 は 51e02a0 の ancestor   = YES
51e02a0 の tree                  = ba3711b6597013df8b268dc764098e7ed68681e6
                                 = 応答 commit の tree と同一
```

**評価された merge ref の tree が応答 tree と同一である。** QF-RVR-MVS01-007 §Reviewed identity で `83857ee` について確認したのと同じ性質が、今回も成立している。CI が checkout した内容と、私がいま読んだ内容は byte 単位で同じである。

---

## 3. RVR-N17〜N22 の再検証

`497d786` を作業コピーへ固定し、静的確認と実行の双方で確かめた。

### RVR-N17 — Stage 6R-10 lane の 85/85

`scripts/ci/write-stage6r10-evidence.py` の `SUITES` に `townReadiness` が登録され、`registeredSuitesComplete` と `totalsMatch` が acceptance 条件に入った。`nativeTotal` を含む文字列は **0 件**である。`check-stage6r10-contract.py` に `include_town_readiness=False` の synthetic 検査が追加された。

**QF-RVR-MVS01-005 で欠陥を示したときと同じ入力を、両 writer へ通した実測結果。**

| writer | 入力 | accepted | 登録 suite | 記録合計 |
|---|---|---|---|---|
| 6R-10 | 完全 90 件ログ | True | 7 | **90** |
| 6R-10 | Town Readiness 欠落 | **False** | 7 | 80 |
| 6R-11 | 完全 90 件ログ | True | 7 | 90 |
| 6R-11 | Town Readiness 欠落 | **False** | 7 | 80 |

以前は同じ 90 件ログに対し 6R-10 writer が `accepted=True` かつ記録合計 85 を返した。**その挙動は再現しない。** 両 lane が同じ入力に同じ判定を返し、欠落を拒否する。

`verification_status: VERIFIED`

### RVR-N18 — 台帳と artifact の自己記述性

`write-stage6r11-evidence.py` が `authoritativeHeadIncluded` と `mergeRefParents` を出力し、`check-stage6r11-contract.py` の検査 10 が `testedTree` を含む 10 token の存在を要求する。taxonomy overlay の 3 Run（`33139913725` / `33139913729` / `33139913757`）が acceptance evidence の「Taxonomy overlay Runs」節へ記録された。

QF-RVR-MVS01-006 §3.2 で求めた「関係を記録する」と「関係を判定する」の分離が、artifact のフィールドとして実現している。

`verification_status: VERIFIED`（Run 識別子そのものは §5 の限定を伴う）

### RVR-N19 — 文書がどの commit に属するか

`review-request.md` に、taxonomy overlay commit `80090e2` と、overlay が移動した file の内容が同一である旨が明記された。

`verification_status: VERIFIED`

### RVR-N20 — 性能 not-run の単一正本

`spec/deferred-tests.json` に `TC-PERF-MVS01-002-PG` が登録された。

```
TC-PERF-MVS01-002-PG | Performance Owner | Before pilot Gate G3 or before the public dataset reaches 100,000 rows, whichever comes first
TC-ACC-MVS01-087-OIDC | System Architect  | VT-1 start
```

`check-test-ids.py` の必須項目強制（`status: not-run`、`reasonCode`、`reason`、`owner`、`due`）が当該 entry にも及ぶ。以前は owner が「Performance Engineer」と「Performance Owner」、due が 2 通りに割れていた。**具体的な条件を持つ側へ統一されている。**

`verification_status: VERIFIED`

### RVR-N21 — RLS 被覆の導出

`TC-ACC-MVS01-066-PG` の被保護表集合が、表名の literal 列挙から `tenant_column.attname = 'tenant_id'` による導出へ変更された。tenant 列を持つ表が RLS 被覆の対象として自動的に含まれる。

`verification_status: VERIFIED`

### RVR-N22 — overlay の記述

`CURRENT_STATE.md` の overlay 説明に次が加わった。

> In addition to path moves, it rewrites the repository entry and navigation
> documents (`README.md`, `CLAUDE.md`, `ARCHITECTURE.md` and `docs/INDEX.md`)
> and strengthens navigation/taxonomy checks.

「path の移動だけ」という以前の記述と、実際の差分の粒度が一致した。

`verification_status: VERIFIED`

### 契約検査の実行

`497d786` の作業コピーで両 contract checker を実行した。

```
python3 scripts/ci/check-stage6r10-contract.py   → exit 0
python3 scripts/ci/check-stage6r11-contract.py   → exit 0
```

---

## 4. 判定

| Finding | 従前 | 本書 |
|---|---|---|
| RVR-N10〜N16 | reviewer 側 `CLOSED_VERIFIED` ＋ owner acceptance 済 | 変更なし |
| RVR-N17 | owner `ACCEPTED`（Claude 再検証なし） | **reviewer 側 `VERIFIED`** |
| RVR-N18 | 同上 | **reviewer 側 `VERIFIED`** |
| RVR-N19 | 同上 | **reviewer 側 `VERIFIED`** |
| RVR-N20 | 同上 | **reviewer 側 `VERIFIED`** |
| RVR-N21 | 同上 | **reviewer 側 `VERIFIED`** |
| RVR-N22 | 同上 | **reviewer 側 `VERIFIED`** |

**新規 Finding は無い。P0／P1 も無い。**

Stage 6R-11R の Closure 判定は Organizer が `2026-08-28` に記録済みであり、本書はそれを変更しない。本書が加えるのは、`final-acceptance.md` が明示的に留保した「second Claude re-verification ではない」という但し書きの解消だけである。

---

## 5. 本書の限界

- 私が確認したのは **git object と、作業コピー上での静的検査・実行**である。
- Run ID `33152117524` / `33152117552` / `33152117623`、Job ID、artifact ID と digest、`authoritativeHeadIncluded: true` が実際の artifact に含まれるかは、**私は確認していない。** 本セッションから GitHub Actions API へ到達できないためである。これらは引き続き packet と Codex の API 検証からの引用である。
- `authoritativeHeadIncluded` については、**writer がそのフィールドを出力すること**を私はコードから確認した。**特定 Run の artifact がその値を `true` として含むこと**は確認していない。両者は別である。

---

## 6. 宣言

- 本書は RVR-N17〜N22 の reviewer 側再検証であり、Stage の Closure 宣言ではない
- 新規 Finding を提起しない
- `docs/reviews/stage6r11r/claude-findings.md` の記載は QF-RVR-MVS01-007 と一致することを確認した
- Repository へ一切書き込んでいない。merge・Draft 解除・branch 操作は行っていない
