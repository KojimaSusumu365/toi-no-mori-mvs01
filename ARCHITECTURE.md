# Architecture boundaries

## 中心原則

「問いが仕事を生み、仕事が経験を生み、経験が次の問いを生む」。システムの主人公はAIではなく、人間の現実の経験から生まれるQuestionです。AIは分解、接続、翻訳、演習化、可視化を支援します。

## Forest–Town境界

| 関心 | Question Forest | Virtual Town |
|---|---|---|
| 所有するRoot | Question | Task |
| 接続 | versioned Read API | Integration Gateway |
| 参照 | UUIDをOpaque Referenceとして公開 | `question_ref`として保持 |
| DB | Forest専用 | Town専用、共有禁止 |
| 本文 | 正本を保持 | 永続保存禁止 |
| title | 正本を保持 | bounded temporary cacheのみ |
| 404 | absent/withdrawnを秘匿して同一応答 | 過去の200と現在の404からunavailableを判断 |
| 429/503/timeout | 一時障害 | unavailableにせずunresolved |

Townが永続化できるのは最低限の参照情報、解決日時、解決結果です。Forestの本文複製を新しい正本にしてはいけません。

## セキュリティ境界

- PostgreSQL RLSは接続プール再利用を前提にする
- tenant GUCは `NULLIF(current_setting('app.tenant_id', true), '')::uuid` として空文字を安全に扱う
- アプリ・マイグレーションroleは `NOBYPASSRLS`
- Public Readは現在single-tenant構成。複数tenant公開前にArchitecture Gateを発動する
- 監査のためにBYPASSRLSを追加しない
- hash chainはtamper-proofではなくtamper-evident

## 未実装を実装済みに見せない

Town runtime、Experience Ledger、Citizen Compute、実環境Sakura failover、実Entra ID、実スマートフォンE2Eは、この文書だけで完成扱いにしません。

## Frozen integration contract

The reviewable Forest–Town rules, storage allowlist, error mapping, and tenant Architecture Gate are fixed in [docs/forest-town-boundary-v1.md](docs/forest-town-boundary-v1.md).
