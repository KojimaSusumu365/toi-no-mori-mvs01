# Claude collaboration guide

このリポジトリでClaudeが最初に読む正本です。目的は「問いの森」の独立レビューであり、Claudeが無断で実装やmergeを進めることではありません。

## 読む順序

1. [CURRENT_STATE.md](CURRENT_STATE.md)
2. [ARCHITECTURE.md](ARCHITECTURE.md)
3. [docs/governance/SOURCE-OF-TRUTH.md](docs/governance/SOURCE-OF-TRUTH.md)
4. [docs/governance/REVIEW-PROTOCOL.md](docs/governance/REVIEW-PROTOCOL.md)
5. [docs/reviews/stage6r11r/review-request.md](docs/reviews/stage6r11r/review-request.md)
6. 対象実装・テスト・既存evidence

全体の文書地図は [docs/INDEX.md](docs/INDEX.md) にあります。

## 現在のreview target

- Repository: `KojimaSusumu365/toi-no-mori-mvs01`
- 累積baseline branch: `stage6r4c-postgresql-green-fix`
- Commit HEAD: `4537085c25ed3178214b0693afac7e42ce1b64de`
- Git tree object: `4402dd93d1a50fe58e96d0fa0242e30cdcc6450e`
- Draft PR: #1
- Review stage: Stage 6R-11R
- Stage 6R-12: 未着手

Commit、tree、PR merge ref、workflow runは別の識別子です。相互に置き換えないでください。

## 不変境界

- Question Forestが上流、Virtual Townが下流
- ForestとTownはDBを共有しない
- QuestionはForestのEntity、TownのAggregate RootはTask
- TownはQuestion本文を永続保存しない
- 既存UUIDをOpaque Referenceとして扱う
- Forestの404は不存在とwithdrawnを区別しない
- 429、503、timeout、DNS障害は失効を意味しない
- 人間が最終判断・承認・責任を持つ
- mainへ直接pushしない。必ずDraft PRとGREEN確認を経る

## Claudeへ依頼する作業

- review target SHAを固定して読む
- 仕様・実装・試験・証跡の不整合をFindingとして提出する
- 推測と確認済み事実を分離する
- 既存Findingの再確認時も、根拠ファイルとSHAを示す
- [review protocol](docs/governance/REVIEW-PROTOCOL.md) の形式を使う

## Claudeが行わない作業

ユーザーの明示承認なしに、mainへのpush、PR merge、Draft解除、branch削除、RLSや監査境界の変更、Findingの自己CLOSEを行わないでください。
