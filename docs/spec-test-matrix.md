# MVS-01 仕様―実装―自動テスト追跡表

基準文書:

- `QF-MVS01-ITR-001` MVS-01反復仕様書 Version 0.1
- `QF-MVS01-TST-001` MVS-01受入試験仕様書 Version 0.1
- `QF-UML-MVS01-001` MVS-01 UML仕様書 Version 0.1
- `QF-UML-MVS01-003` MVS-01 Stage 3災害復旧 UML仕様書 Version 0.3
- `QF-UML-MVS01-004` MVS-01 Stage 4スマートフォン/BFF UML仕様書 Version 0.4
- `QF-UML-MVS01-005` MVS-01 Stage 5 OIDC実プロトコル UML仕様書 Version 0.5
- `QF-MVS01-S6-001` MVS-01 Stage 6 最小業務フロー詳細仕様書 Version 0.6
- `QF-UML-MVS01-006` MVS-01 Stage 6 業務フロー UML仕様書 Version 0.6
- `QF-ST6R2-MVS01-001` MVS-01 Stage 6R-2 Domain層 赤→緑 仕様 Version 0.1
- `QF-UML-MVS01-6R2` MVS-01 Stage 6R-2 Domain層 UML仕様書 Version 0.1
- `QF-ST6R3-MVS01-001` MVS-01 Stage 6R-3 承認API 赤→緑 仕様 Version 0.1
- `QF-UML-MVS01-6R3` MVS-01 Stage 6R-3 承認API UML仕様書 Version 0.1
- `QF-ST6R4-MVS01-001` MVS-01 Stage 6R-4 テナント境界 赤→緑 仕様 Version 0.1
- `QF-UML-MVS01-6R4` MVS-01 Stage 6R-4 テナント境界 UML仕様書 Version 0.1
- `QF-ST6R4C-MVS01-001` MVS-01 Stage 6R-4C 非root PostgreSQL CI仕様書 Version 0.1
- `QF-ST6R5-MVS01-001` MVS-01 Stage 6R-5 Draft PR受入・全体回帰仕様 Version 0.1
- `QF-ST6R6-MVS01-001` MVS-01 Stage 6R-6 Platform Security監査境界仕様 Version 0.1
- `QF-UML-MVS01-6R6` MVS-01 Stage 6R-6 Platform Security監査 UML仕様書 Version 0.1
- `QF-ST6R7-MVS01-001` MVS-01 Stage 6R-7 DB追記専用・改ざん防止境界仕様 Version 0.1
- `QF-UML-MVS01-6R7` MVS-01 Stage 6R-7 DB追記専用・改ざん防止 UML仕様書 Version 0.1
- `ADR-0003` 石狩本番・東京復旧と暗号化論理バックアップ
- `ADR-0004` スマートフォンWeb UIと同一オリジンBFF/OIDC
- `ADR-0005` Managed IdP候補とOIDC実プロトコルE2E
- `ADR-0006` スマートフォン業務ワークスペースと管理閲覧境界

## 実装済み追跡表

| 要求ID | 実装境界 | 対になる自動テスト | 状態 |
|---|---|---|---|
| REQ-MVS01-IAM-001 | ASP.NET Core authorization policy、Testing限定認証 | API TC-002、TC-011 | 実装済み |
| REQ-MVS01-IAM-002 | Production必須OIDC、Authorization Code + PKCE | API TC-034/035 | 汎用BFF境界を実装済み。実IdP接続は未実施 |
| REQ-MVS01-IAM-003 | 管理操作に設定済みMFA証跡claimを必須化 | API TC-036、OIDC E2E TC-044 | 試験IdPで実装済み。実IdPのclaim mappingは未実施 |
| REQ-MVS01-IAM-004 | HTTPS OIDC discovery/authorize/token/JWKS/logout、PKCE、nonce、recent auth | OIDC E2E TC-043/047/048 | 試験IdPで実protocol検証済み。実Entra tenantは未接続 |
| REQ-MVS01-QST-001 | `Question`生成、入力検証、作成API | Domain TC-003、API TC-003/004 | 実装済み |
| REQ-MVS01-QST-002 | 所有者ガード、version、`If-Match` | Domain TC-006/007、API TC-006/007 | 実装済み |
| REQ-MVS01-WF-001 | `DRAFT → IN_REVIEW`、未定義遷移拒否 | Domain TC-008/009、API TC-009 | 実装済み |
| REQ-MVS01-WF-002 | 差戻し、Reviewer、自己承認禁止、公開日時 | Domain TC-010/012/013、API TC-010/011 | 実装済み |
| REQ-MVS01-PUB-001 | 公開専用DTO、`PUBLISHED`限定 | API TC-014 | 実装済み |
| REQ-MVS01-SRH-001 | キーワード・タグ検索、安定順序 | API TC-015 | 最小実装済み。カーソルは次反復 |
| REQ-MVS01-WD-001 | `PUBLISHED → WITHDRAWN`、公開404 | Domain TC-016、API TC-017 | 実装済み |
| REQ-MVS01-AUD-001 | 許可リスト型の追記監査、Auditor専用・tenant限定・上限付き取得 | API TC-023/072-API、Mobile TC-055、PostgreSQL TC-025 | Reviewer拒否、他tenant不可視、旧無制限経路廃止まで実装済み |
| REQ-MVS01-AUD-002 | tenant外拒否監査、要求/相関ID、429抑制、PlatformAuditor期間照会 | API TC-070/071/080、PostgreSQL TC-071 | Run #1でAPI 40/40、PostgreSQL 11/11を含む全80/80 GREEN |
| REQ-MVS01-DAT-001 | advisory lock付き埋込みマイグレーション | PostgreSQL TC-024 | 実装済み |
| REQ-MVS01-DAT-002 | 状態・監査・冪等結果の原子的確定 | PostgreSQL TC-025 | 実装済み |
| REQ-MVS01-AVL-001 | プロセス再起動後の公開データ保持 | PostgreSQL TC-026 | 実装済み |
| REQ-MVS01-AVL-002 | DB障害時のreadiness/Data API 503と秘匿 | PostgreSQL TC-027 | 実装済み |
| REQ-MVS01-MOB-001 | 同一オリジンWeb UI、360px、44px操作、safe area | Mobile TC-039/042 | 構造・実Kestrel配信を実装済み。実機は未検証 |
| REQ-MVS01-MOB-002 | landmark、label、skip link、focus、live region | Mobile TC-040 | 構造検査済み。screen reader実機は未検証 |
| REQ-MVS01-UI-001 | Editor本人の一覧、作成、版付き編集、申請、差戻し理由 | API TC-049、Mobile TC-055 | スマートフォン画面と所有者限定APIを実装済み |
| REQ-MVS01-UI-002 | Reviewer queue、公開中一覧、差戻し、承認、取り下げ | API TC-050/054、Mobile TC-055 | ReviewerとAuditorの画面境界を分離済み |
| REQ-MVS01-UI-003 | 異なるEditor/Reviewerによる作成から公開までの完結 | API TC-052、OIDC E2E TC-057 | API受入と実HTTPS署名sessionの両方で検証済み |
| REQ-MVS01-UI-004 | 理由付き差戻し、Editor再編集、理由消去 | API TC-053 | 実装済み |
| REQ-MVS01-SEC-001 | JSONエンコード、CSRF、tenant/所有者、冪等、レート制限 | API TC-005/018/019/020/021/023/069 | tenant越境・他所有者を同じProblem Details 404へ正規化済み |
| REQ-MVS01-SEC-002 | ProductionでPostgreSQL・TLS証明書/ホスト名検証を強制 | API TC-028/029 | 実装済み |
| REQ-MVS01-SEC-003 | HttpOnly Cookie、token非保存、login-bound CSRF、同一アプリredirect | API TC-035/037/038、OIDC E2E TC-046/047 | 試験IdPまで実装済み。実IdP logoutは未検証 |
| REQ-MVS01-SEC-004 | 同一origin、CSP、`no-store`、安全なDOM描画、外部resourceなし | Mobile TC-041/042 | 実装済み |
| REQ-MVS01-SEC-005 | RSA署名、issuer/audience/lifetime、MFA、`auth_time`、組織claim、暗黙login禁止 | OIDC E2E TC-044/045/048、API TC-065 | 試験claim許可表はGREEN。実tenant claim受入は未実施 |
| REQ-MVS01-SEC-006 | 管理閲覧のMFA/role/所有者境界、ETag、CSRF、冪等、理由長上限 | API TC-051/059、Mobile TC-056、PostgreSQL TC-058 | Store/SQL段階の所有者制約と画面更新保護を実装済み。TC-058は今回未実行 |
| REQ-MVS01-DR-001 | 石狩本番、東京コールド復旧、GSLB手動切替の境界 | UML-DPL-MVS01-003、runbook review | 設計済み。実クラウド構築は未実施 |
| REQ-MVS01-DR-002 | CAdES/SHA-256署名、CMS/AES-256-GCM公開鍵暗号、内部manifest | DR TC-030 | ローカル実DBで実装済み |
| REQ-MVS01-DR-003 | GCM認証、固定署名者証明書、内部dump SHA-256 | DR TC-031 | 1バイト改ざん拒否を実装済み |
| REQ-MVS01-DR-004 | 旧DB停止、別data directory、空DB復元、API/監査照合 | DR TC-032 | ローカル隔離復元を実装済み |
| REQ-MVS01-DR-005 | snapshot/災害宣言/受入UTCからRPO・RTO算出 | DR TC-033 | 暫定目標RPO 1h/RTO 4hを機械判定 |

## Stage 6R-2 Domain追跡表

| 設計根拠 | 実装境界 | 対になる自動テスト | 状態 |
|---|---|---|---|
| ADR-0008-D1/D2 | 版付き`Approve`、`ApprovedVersion`、`ApprovedBy` | Domain TC-063-DOM | native挙動試験でGREEN。API接続はStage 6R-3で実装 |
| DOMAIN-INVARIANTS | `TenantId`、Snapshot、command原子性 | Domain TC-079-DOM | seed固定500×20操作でGREEN。RLS実装はStage 6R-4、実DB未実行 |
| ADR-0008-D4 | `ReviewReason`と`WithdrawalReason`の分離 | Domain TC-081-DOM | DomainはGREEN。role別API DTOは未実装 |

## Stage 6R-3 承認API追跡表

| 設計根拠 | 実装境界 | 対になる自動テスト | 状態 |
|---|---|---|---|
| ADR-0008-D1 | 承認Endpoint strong `If-Match`、428/400/409、成功ETag | API TC-064-API | native挙動試験でGREEN |
| ADR-0008-D1/D5 | `ApproveAsync(expectedVersion)`、版を含む冪等指紋 | API TC-064-API/018 | InMemoryでGREEN。実PostgreSQLは未実行 |

## Stage 6R-4 テナント境界追跡表

| 設計根拠 | 実装境界 | 対になる自動テスト | 状態 |
|---|---|---|---|
| ADR-0007-D2 | `external_organization_id`許可表、内部tenant context、欠落/未登録403 | API TC-065-API | native挙動試験でGREEN |
| RV-021 | 他所有者・他tenant・不存在のProblem Details 404正規化 | API TC-069-API | status/type/title一致とID非開示をGREEN |
| ADR-0007-D1/D3 | tenant列、transaction-local `set_config`、RLS ENABLE/FORCE、非owner applicationロール | API TC-066-API、PostgreSQL TC-066/067-PG | 2接続・異role・双方VerifyFull・最小GRANT・Production起動診断を実装。GitHub Run #4でnative PG 10/10 GREEN |
| ADR-0007-D4/RVA-C05 | revisionと公開revisionの同一tenant・同一question複合FK | PostgreSQL TC-068-PG | native試験build済み、実DB未実行 |
| ADR-0008-D5/RV-023 | tenant・actor・対象版・期限付き冪等scope | PostgreSQL TC-074-PG | native試験build済み、実DB未実行 |
| Migration 002/003 | Expand/Contract、tenant default撤去、revision初期化 | PostgreSQL TC-075-PG | native試験build済み、実DB未実行 |
| QF-ST6R4C-MVS01-001 | Ubuntu 24.04非root CI、必須DB gate、機械判定証跡 | CI構成契約6件＋PostgreSQL native 10件 | GitHub Actions Run #4で10/10 GREEN、artifact SHA-256確認済み |

## Stage 6R-5 Draft PR受入追跡表

| 設計根拠 | 実装境界 | 対になる自動テスト | 状態 |
|---|---|---|---|
| ADR-0009-D7 / REQ-MVS01-AUD-001 | Auditor policy、`/api/ops/audit`、tenant・limit・許可リストDTO | API TC-072-API、Mobile TC-055、PostgreSQL TC-025 | ローカルAPI/Mobile GREEN。実DB回帰はCI gateで確認 |
| QF-ST6R5-MVS01-001 | 非root full regression wrapper、exact-count evidence | CI構成契約8件、native 76件 | GitHub Actions Run #5で76/76 GREEN、artifact digest確認済み |
| ADR-0003 / REQ-MVS01-DR-002〜005 | 分離application roleで暗号化backup・隔離復元 | DR TC-030〜033 | Run #5で4/4 GREEN |

## Stage 6R-6 Platform Security監査追跡表

| 設計根拠 | 実装境界 | 対になる自動テスト | 状態 |
|---|---|---|---|
| ADR-0009-D5/D6 | `X-Correlation-ID`と要求ごとの`X-Request-ID`、安全な再生成 | API TC-070-API | RED→GREEN、API 40/40 |
| ADR-0009-D1 / ADR-0010-D2 | 拒否envelope、HMAC partition、UTC 1分429抑制、期間必須PlatformAuditor API | API TC-071-API | RED→GREEN |
| ADR-0009-D8 | bounded queue、sink timeout、fallback metric/log、元応答維持 | API TC-080-API | RED→GREEN |
| ADR-0010-D1 / RVR-N01 | migration 004、platform表、application/writer/reader権限分離 | PostgreSQL TC-071-PG | Run #1で11/11 GREEN |
| QF-ST6R6-MVS01-001 | exact-count 80件、非root native、immutable evidence | CI構成契約6件、native 80件 | Run #1で80/80 GREEN、artifact digest確認済み |

## Stage 6R-7 DB追記専用・改ざん防止追跡表

| 設計根拠 | 実装境界 | 対になる自動テスト | 状態 |
|---|---|---|---|
| ADR-0009-D9 | `audit_events`、`platform_security_events`、`question_revisions`の権限・trigger二重境界 | PostgreSQL TC-073-PG | Run #1で期待RED、Run #3でPostgreSQL 12/12 GREEN |
| QF-ST6R7-MVS01-001 | exact-count 81件、非root native、immutable evidence | CI構成契約6件、native 81件 | Run #3で81/81 GREEN、artifact digest確認済み |

## テスト層の役割

| 左側成果物 | 同じ変更で更新する実装 | 右側テスト |
|---|---|---|
| UML-CLS-MVS01-001 | `Question.cs` | Domain TC-003/006/007 |
| UML-SM-MVS01-001 | `Question`状態メソッド | Domain TC-008〜016 |
| UML-SEQ-MVS01-002 | 作成・更新API、ETag | API TC-003/006/007 |
| UML-SEQ-MVS01-003 | 承認、冪等、監査 | API TC-010/011/018/023 |
| UML-SEQ-MVS01-004 | 公開詳細・検索・取り下げ | API TC-014/015/017/021 |
| UML-CMP/CLS-MVS01-002 | `IQuestionStore`、PostgreSQL、マイグレーション | PostgreSQL TC-024/025 |
| UML-SEQ-MVS01-005 | 永続化・アプリ再起動 | PostgreSQL TC-026 |
| UML-SEQ-MVS01-006 | DB障害と安全側503 | PostgreSQL TC-027 |
| UML-DPL-MVS01-003 | 石狩本番、東京復旧、鍵分離、CRR | ADR-0003、runbook。実クラウド試験は次反復 |
| UML-SEQ-MVS01-007 | dump、署名、暗号化、CRR照合 | DR TC-030 |
| UML-SEQ-MVS01-008 | 改ざん検出とDB操作前拒否 | DR TC-031 |
| UML-SEQ-MVS01-009 | 本番停止、隔離復元、API/監査、RPO/RTO | DR TC-032/033 |
| UML-DPL-MVS01-004 | スマートフォン、同一origin BFF、OIDC、共有key ring | ADR-0004、API TC-034/035、Mobile TC-042 |
| UML-SEQ-MVS01-010 | OIDC code + PKCE、MFA、Cookie、最小session | API TC-034〜038 |
| UML-SEQ-MVS01-011 | CSRF付き下書き作成、安全なDOM描画 | API TC-036/037、Mobile TC-041/042 |
| UML-TST-MVS01-004 | Stage 4 V字対応 | API TC-034〜038、Mobile TC-039〜042 |
| UML-SEQ-MVS01-012 | OIDC code + PKCE、token、JWKS、nonce、Cookie | OIDC E2E TC-043 |
| UML-SEQ-MVS01-013 | 署名/MFA/auth_time不正の安全側拒否 | OIDC E2E TC-044/045/048 |
| UML-SEQ-MVS01-014 | 実OIDC sessionのCSRF更新とlogout | OIDC E2E TC-046/047 |
| UML-TST-MVS01-005 | Stage 5 V字対応 | OIDC E2E TC-043〜048 |
| UML-UC/NAV-MVS01-006 | Editor/Reviewer/Auditor/Publicとrole別画面遷移 | API TC-049/050/072、Mobile TC-055 |
| UML-SEQ-MVS01-015 | 異なるOIDC利用者による作成・申請・承認・公開 | API TC-052、OIDC E2E TC-057 |
| UML-SEQ-MVS01-016 | 差し戻し理由と版付き再編集 | API TC-053 |
| UML-SEC-MVS01-006 | 管理閲覧の認証・MFA・role・所有者境界 | API TC-051/054、Mobile TC-056、PostgreSQL TC-058 |
| UML-TST-MVS01-006 | Stage 6 V字対応 | API TC-049〜054/059、Mobile TC-055/056、OIDC E2E TC-057、PostgreSQL TC-058 |
| UML-CLS/SM/SEQ-MVS01-6R2 | 承認対象版、tenant不変、理由分離 | Domain TC-063-DOM/079-DOM/081-DOM |
| UML-CMP/SEQ-MVS01-6R3 | If-Match、版付きStore、冪等再送、応答ETag | API TC-064-API |
| UML-CMP/SEQ/ER-MVS01-6R4 | 組織claim許可表、tenant伝搬、RLS、複合FK、正規化404 | API TC-065/069、PostgreSQL TC-066/067/068/074/075 |
| UML-CMP/SEQ/DPL-MVS01-6R6 | request/correlation、非同期監査、429抑制、platform DB role分離 | API TC-070/071/080、PostgreSQL TC-071 |
| UML-CMP/SEQ/TST-MVS01-6R7 | 追記専用GRANT、3 mutation trigger、owner操作拒否 | PostgreSQL TC-073 |

Pull Requestの完了条件は、要求ID、UML ID、実装、Domain/API/Mobile/OIDC E2E/PostgreSQL/DRテストIDのリンクが同じ変更内で維持されることです。Stage 6R-7 gateはDomain 12、API 40、Mobile 6、OIDC E2E 7、PostgreSQL 12、DR 4の全81件をexact-countで要求します。Stage 6R残存5件、実IdP、実browser/スマートフォン、さくら実クラウドの判定は別gateです。
