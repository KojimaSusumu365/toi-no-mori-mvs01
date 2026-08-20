# MVS-01 Stage 6R-4 検証結果

- 検証日: 2026-08-20
- SDK/runtime: .NET SDK 10.0.400 / .NET・ASP.NET Core runtime 10.0.11 / C# 14
- Web/Auth: ASP.NET Core Minimal API / OpenID Connect package 10.0.10
- DB/driver: PostgreSQL 18.6 toolchain / Npgsql 10.0.3
- 構成: Release

## Stage 6R-4 最新判定

| 検査 | 今回の結果 |
|---|---|
| Solution Release build | 合格。警告0、エラー0 |
| 試験ID一意性 | 合格 |
| Domain仕様テスト | 12/12 合格 |
| API/BFF仕様テスト | 36/36 合格。TC-065/066-API/069を含む |
| Mobile Web仕様テスト | 5/6。承認済みAuditor仕様に対する既知TC-055だけRED |
| OIDC browser protocol E2E | 7/7 合格。試験IdPは組織claimを発行 |
| 今回実行したnative test | 60/61 合格 |
| PostgreSQL実DB統合テスト | 10件build合格、0件実行。Stage 6R-4新規5件は未合格 |
| Stage 6R-4C CI構成契約 | 6/6 合格。action SHA固定・read-only・非root/件数判定を確認 |
| Stage 6R-4C root失敗閉鎖 | 合格。native suite未開始、exit 2、accepted=false |
| Stage 6R残存契約 | 11/11 expected RED、harness error 0 |
| DR暗号化・隔離復元 | 4件未実行 |
| T3追跡総数 | 承認済み85件＋補助TC-066-API 1件＝86件。未完了 |

APIの失敗先行では、実装前に既存33件が合格し、TC-065はclaim欠落でも201、TC-069は他所有者が403となって2件が赤だった。実装後は35/35。追加ループで404本文差も除去し、他所有者・他tenant・不存在を同じProblem Detailsへ固定した。

PostgreSQL 18.6のbinaryと10件のtest assemblyは存在する。`./scripts/test-postgresql.sh`を実行したが、このWork環境はrootから`nobody`への実効UID変更を禁止し、PostgreSQLはroot起動を拒否するため、initdb前にexit 2で安全停止した。root guardを外さず、実行結果をGREENへ数えていない。

DBロール反復ではTC-066-APIを失敗先行で追加し、既存35件合格・新規1件失敗を確認した。application/migration接続分離、異なるusername、双方の`VerifyFull`、最小GRANT、applicationロールの`NOINHERIT`・非owner・非superuser・非`BYPASSRLS`・schema `CREATE`なし・migration ledger権限なしの起動時診断を実装し、API 36/36へGREEN化した。TC-066-PGにはsuperuser・owner・`BYPASSRLS`候補の拒否も追加した。

ただし実DB runnerは引き続きinitdb前にexit 2で停止する。したがってロール境界の実装は完了したが、PostgreSQL 10件の実行結果を得るまでDB gateは未合格である。

Stage 6R-4CではGitHub Actions workflow、非root実行ラッパー、JSON/Markdown/log証跡生成、CI構成契約6件を追加した。ローカルでは6/6とroot失敗閉鎖を確認したが、GitHub repository上のrunは未実行であり、native 10件の合格数は0のままである。

## Stage 6 基準時点の履歴

| 検査 | 今回の結果 |
|---|---|
| Solution Release build | 合格。警告0、エラー0 |
| JavaScript構文検査 | 合格（`node --check`） |
| Domain仕様テスト | 9/9 合格 |
| API/BFF仕様テスト | 32/32 合格 |
| Mobile Web仕様テスト | 6/6 合格 |
| OIDC browser protocol E2E | 7/7 合格 |
| 今回実行合計 | 54/54 合格 |
| PostgreSQL実DB統合テスト | 5件のテストassemblyは再ビルド合格。実行環境のUID変更禁止により今回は未再実行 |
| DR暗号化・隔離復元テスト | PostgreSQL起動前提のため今回は未再実行 |
| 全層の予定件数 | 63件（54 + PostgreSQL 5 + DR 4） |

PostgreSQL/DRを未実行のまま63/63合格とは記載しない。Stage 5では両層各4件が合格している。Stage 6では管理一覧のSQL行scopeを検査するTC-058を加えたため、非特権PostgreSQL processを起動できるCIまたは検証機で5件を実行する。

## Stage 6で確認した業務・安全境界

- Editorの管理一覧は本人所有の問いだけを返す。
- 他Editorによる管理詳細のID指定は404とし、存在を列挙させない。
- Reviewerはレビュー待ちと公開中の問いを取得できる。
- スマートフォン画面に編集、審査、監査のrole別領域を追加した。
- 更新はCSRF、`If-Match`、所有者、状態、roleをサーバー側で検査する。
- 承認は`Idempotency-Key`を必須とし、自己承認を画面とDomain/APIの両方で拒否する。
- 差し戻し理由をEditorへ表示し、修正保存後に消去する。
- 異なる署名済みOIDC Editor sessionとReviewer sessionで、作成、申請、一覧、承認、匿名公開まで完結した。
- access token、ID token、client secretをBFF session JSON、Cookie、DOM、Web Storageへ保存しない既存境界を維持した。

## TC-049〜059

- TC-049: Editor一覧を本人所有だけに限定する。
- TC-050: Reviewerが`IN_REVIEW`一覧を取得する。
- TC-051: 管理詳細を所有者またはReviewerに限定し、ETagを返す。
- TC-052: API受入で作成、編集、申請、承認、公開を完結する。
- TC-053: 差し戻し理由を再作業へ引き継ぎ、修正後に消去する。
- TC-054: 未定義の管理一覧状態を400にする。
- TC-055: 編集、審査、監査のrole別スマートフォン画面構造を検査する。
- TC-056: UI更新のCSRF、If-Match、冪等キー、自己承認表示を検査する。
- TC-057: 二つの独立Cookie containerと実HTTPS試験IdPを使い、異なるEditor/Reviewerで公開まで往復する。
- TC-058: PostgreSQLの管理一覧をEditor所有者scopeとReviewer scopeに分離する。
- TC-059: 1000文字を超える審査理由を永続化前に400で拒否する。

## 自己ループ記録

1. Stage 6要求、管理閲覧API、Editor/Reviewer/監査画面、TC-049〜059を同じ反復で追加した。
2. 静的解析を警告=エラーで実行し、OIDC test helperのstatic化と配列再利用を修正した。
3. 第1テストループでレート制限試験が後続の公開受入試験を429にする試験順序依存を検出した。
4. レート制限試験を最後へ移動し、業務試験同士の独立性を回復した。
5. API 32、Mobile 6、OIDC E2E 7、既存Domain 9の全54件を再実行し、合格した。
6. PostgreSQL/DR再試験を開始したが、sandboxが`runuser`による別UID起動を禁止したため`initdb`前に停止した。製品コードの不合格として扱わず、未実行として記録した。

## 実行環境に関する注記

OIDC E2Eは、一時self-signed certificateを個別にpin留めした実Kestrel HTTPS endpoint、署名付き試験IdP、独立Cookie containerを使用する。redirect、Cookie、back-channel HTTP、PKCE、JWKS、nonceを実行するが、Chromium/WebKit等のrendering engineや物理スマートフォンは使用しない。

今回のsandboxはUID変更を禁止する。通常のPostgreSQL試験scriptはroot時に`nobody`へ切り替える安全設計のため、`runuser: cannot set groups`で停止した。root guardを無効化する改造や、製品成果物への試験専用例外は追加していない。

## Stage 6R-4 未達事項

- 実Entra tenantのapp registration、app roles、Conditional Access、MFA・組織claim受入
- iOS Safari、Android Chrome、desktop Chromium/WebKitとscreen readerによるE2E
- PostgreSQL実DB統合10件、DR4件の再実行
- 構築済みGitHub Actionsをrepository上で実行し、分離ロールによるnative 10/10証跡を取得してrequired check化
- platform security events、拒否監査envelope、tenant Auditor/PlatformAuditor権限分離
- 公開APIの複数tenant向けhost/path解決。現在は移行tenant MVS-01へ固定
- さくらLoad Balancerのproxy trust、versioning、CRR、GSLB、東京復旧訓練
- PostgreSQL standby、WAL archive/PITR、backup保持・削除policy
- 監視通知、IaC、秘密管理・鍵rotation、CSP report収集

ローカル60件合格は本番SLAやProduction承認を意味しない。Product Owner、Architecture、Test Lead、Security Reviewerによるレビューと、未実行gate、実IdP、実端末、実クラウドの受入を別途必要とする。
