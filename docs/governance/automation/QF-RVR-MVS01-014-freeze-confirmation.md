# QF-OPS-MVS01-001 Version 0.5.1 — 設計凍結の確認

- 文書ID: QF-RVR-MVS01-014
- 版: Version 0.1
- 日付: 2026-08-28
- 対象: QF-OPS-MVS01-001 Version 0.5.1（FINAL DESIGN FREEZE）
- 応答主体: 外部レビュー側（Claude）
- `review_mode`: `INITIAL`
- 種別: **凍結確認のみ。** 新規 Finding の提起を目的としない

---

## 0. 判定

**AUTO-P2-35、AUTO-P2-36、AUTO-P3-37 の 3 件すべてが設計へ反映されている。**

**新規 P0／P1 は無い。**

§20 の凍結規則に照らし、**Version 0.5.1 の設計凍結を妨げる事由は reviewer 側に無い。**

本書は意図的に短い。§20 は「追加 P2／P3 は実装 backlog へ記録し、本仕様の新版作成理由にしない」と定めた。**その規則は、私が新しい P2／P3 を探し続けないことによってのみ機能する。** 5 版で 37 件を提起した経緯からすれば、6 版目でも数件は出せる。出さないことが、凍結に対する reviewer 側の履行である。

---

## 1. 反映確認

| Finding | 反映箇所 | 確認 |
|---|---|---|
| AUTO-P2-35 | §9.7 Durable Review Result publication、§4.2 denylist（`docs/evidence/automation/reviews/**`）、§15 `retention_days` / `review_results_durable_path`、T36 | **反映。要求以上** |
| AUTO-P2-36 | §3.4 手順 5、§12 停止条件、§15 `review_events`、§17 Step 2.5、T37 | **反映。要求以上** |
| AUTO-P3-37 | §3.5 Required Check の適用条件（新設）、§12、§15 `applicability_default`、T38 | **反映。要求以上** |

試験件数を検算した。**8＋2＋11＋18＝39。記載と一致する。**

### 1.1 要求を超えた 3 点

**AUTO-P2-35 — 書込み経路の設計。** 私は保存先の定義だけを求めた。Version 0.5.1 は書込み主体まで設計している。`qf-evidence-publisher` は AI 資格情報を持たず、PR code を実行せず、default branch へ直接 push せず、専用 evidence branch へ新規 file だけを追加して Draft PR を作る。人が merge して初めて永続 Record になる。**永続化のために新しい write 経路を Control Plane へ開かない**という制約を守ったうえで解いている。

あわせて「Draft PR 上の Record、Actions Artifact、workflow log は transport または一時証拠であり、`REVERIFY` または Disposition Record の長期参照正本にしない」と明記された。**証跡の「正本」と「輸送物」を分けた点が本質である。**

**AUTO-P2-36 — `ready_for_review` の追加。** 私は `pull_request_review` の 3 種を挙げた。Version 0.5.1 は `pull_request` 側の `ready_for_review` も再評価契機に含めている。Draft から Ready への遷移は commit を生まないが PR の状態は変わる。**私が挙げなかった経路である。**

**AUTO-P3-37 — pagination 完了の検査。** 私は「変更 file 一覧が空または判定不能なら RED」までしか書かなかった。Version 0.5.1 は `pagination` 完了を独立の判定項目とし、rename 前 path と API 取得件数まで Check Result へ残す。**変更 file が多い PR で一覧が途中までしか取れず、appointment record を見落とす**という経路は、私の required_change では塞げていなかった。

---

## 2. 新規 P0／P1 の有無

凍結規則（§20-2）に従い、P0／P1 のみを対象に確認した。**該当なし。**

主要な不変条件が相互に矛盾していないことを、次の観点で確認した。

| 観点 | 結果 |
|---|---|
| Control Plane が default branch に固定され、製造物から分離されている | 成立 |
| write 資格情報を持つ Job が LLM 生成コードを実行しない | 成立 |
| 特権 workflow が fork 由来 event で secret・artifact・write に到達しない | 成立 |
| LLM 出力が値域と mode 選択の双方で外部から制約されている | 成立 |
| 永続台帳が期限付き証跡へ依存していない | 成立（AUTO-P2-35 反映後） |
| 人間の第二署名が機械強制され、取消に追随する | 成立（AUTO-P2-36 反映後） |
| 検査集合の消失が機械検出される（Gate／Precondition／Stop の 3 Registry） | 成立 |
| 新しい write 経路が Control Plane へ開かれていない | 成立 |

---

## 3. reviewer 側の次の行動

§20 の定めに従う。

- **本仕様の新版はレビューしない。** 次に受け取るのは固定実装 SHA である
- 実装レビューでは `review_mode` を私が選択しない。ただし Review Request Job が未実装の段階では、§20 と同じく Organizer が mode を指定する
- 実装 Finding の ID は `AUTO-IMPL-*` とし、`.github/ai/registries/finding-ids.yml` の登録に従う
- AUTO-P0-01〜P3-37 の 37 件は、いずれも `verification_status: OPEN` のままである。**設計反映は実装検証ではない**

---

## 4. 宣言

- `review_mode`: `INITIAL`。本書は自動系の trusted Review Request ではなく、実装 `VERIFIED` 証跡でもない
- 新規 Finding を提起しない。P0／P1 が無いためであり、探索を打ち切ったためではない
- `disposition` は設定しない
- Repository へ一切書き込んでいない
