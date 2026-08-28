# Roadmap

| 順序 | Stage | 目的 | Exit |
|---:|---|---|---|
| 1 | 6R-11R | 外部reviewと実装・試験・証跡の整合 | Finding closure + user acceptance |
| 2 | Automation bootstrap | Controller実装・39試験・Claude/独立人間review | Organizerが別途有効化を承認 |
| 3 | 6R-12 | Question Forest Minimum v1 RC | RC gate GREEN |
| 4 | QF v1 | 「問いの森」最小運用版 | 実在Questionを安全に扱える |
| 5 | VT-X0 | Questionから訓練Taskへの変換実験 | A/B二題とObservable Fact Sheet |
| 6 | Virtual Town Minimum | Task、役割、参加、受入の最小実装 | Forest本文を所有しない境界試験 |
| 7 | Forest–Town integration | Read API / Gateway / Outbox接続 | unavailable/unresolvedを含む契約試験 |
| 8 | Experience Ledger | 成功・失敗・訂正を証拠化 | 消去可能payloadと不変coreの分離 |
| 9 | Citizen Compute | Question由来の計算需要だけを支援 | 医療・安全境界を満たす |

一度にTown全体を作らず、Questionから必要になったTask、Role、Organizationだけを下流へ育てます。各Stageは独立Draft PR、固定SHA、機械gate、外部review、ユーザー承認で閉じます。

Automation bootstrapはStage 6R-12の開始許可ではありません。Reviewerが
`VACANT`の間は実装・静的試験までに留め、mergeとPhase Aを開始しません。
