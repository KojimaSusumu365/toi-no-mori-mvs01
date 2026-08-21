# ADR-0006 スマートフォン業務ワークスペースと管理閲覧境界

- Status: Accepted; Auditor boundary amended in Stage 6R-5
- Date: 2026-08-16

## Context

Stage 5では公開検索と下書き作成だけが画面操作可能であり、レビュー、差し戻し、承認、取り下げ、監査はAPIだけだった。実クラウドへ配置する前に、異なるEditor/Reviewerがスマートフォンから業務価値を完結できる必要がある。一方、SPA用access tokenをブラウザへ持たせる方式、他Editorの下書きを取得して画面側で隠す方式、端末Web Storageへ業務データを保存する方式は採用できない。

## Decision

- 既存の同一オリジンHTML/CSS/JavaScriptとBFF Cookieを継続する。
- Editor、Reviewer、Auditorをroleで表示する単一ワークスペース内のタブとする。
- 管理一覧・詳細APIを追加し、Editorの所有者制約をStore/SQL段階で適用する。
- 更新はCSRF、`If-Match`、状態規則、role、所有者をサーバーで再検査する。
- 承認はブラウザで一意な`Idempotency-Key`を作り、Storeで一回だけ確定する。
- DOMは要素生成と`textContent`だけで更新し、外部resource、inline code、Web Storageを使用しない。
- Reviewer roleだけでは監査画面・監査APIを利用できない。tenant Auditorへだけ、1〜200件に制限した許可リスト型の操作metadataを表示する。
- 監査APIは`/api/ops/audit`へ分離し、旧`/api/admin/audit`を廃止する。

## Consequences

### Positive

- tokenをJavaScriptへ渡さずに、スマートフォンで業務を完結できる。
- EditorのBOLA対策をUIではなく問い合わせ境界で強制できる。
- 仕様とAPI/Mobile/OIDC E2Eを一対一に追跡できる。
- 将来のEntra ID接続時もgeneric OIDC契約を維持できる。

### Trade-offs

- 単一HTML/JavaScriptの規模が増えるため、次の大型反復ではmodule分割を検討する。
- access tokenを持たないため、BFF障害時に画面単独で管理操作はできない。これは安全側の設計とする。
- 物理端末とbrowser engineの見た目・支援技術は静的契約試験だけでは保証できない。

## Rejected alternatives

- Browser Local Storageへtokenや下書きを保存する: XSS、端末共有、session失効との不整合がある。
- Reviewerに全件を返しEditorは画面で絞る: API応答で他者データが漏れる。
- 自己承認をボタン無効化だけで防ぐ: API直接呼出しを防げない。
- 実クラウド配置を先行する: 未完の業務画面を運用環境へ持ち込むことになる。
