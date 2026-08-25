# QF-TP-MVS01-001 修正済みテスト計画 追補 v0.2

- 日付: 2026-08-20
- 変更理由: 外部レビュー応答 QF-RVR-MVS01-001 のRVR-N01/N02
- 基準: 新規22証跡・予定85件という件数は変更しない

> Stage 6R-4 DBセキュリティ追補: Production構成だけで検査できる接続分離を、TC-066-PGの代替ではなく補助試験`TC-ACC-MVS01-066-API`として追加した。承認済み予定85件は変更せず、実行・追跡上は補助1件を併記して合計86件とする。補助試験のGREENをTC-066-PGの実DB合格へ読み替えない。

## 変更1: TC-ACC-MVS01-071-PG

旧記述の「限定reasonでは`audit_events.tenant_id NULL`を許す」を廃止する。合格条件を次へ置換する。

1. tenant内の`audit_events.tenant_id`は常にNOT NULLである。
2. tenant未確定の拒否は`platform_security_events`へだけ記録される。
3. 専用表へ書けるreasonは`access.unauthenticated`、`tenant.claim_missing`、`tenant.claim_invalid_or_unmapped`の許可集合に限定される。
4. tenant application role/Auditorは専用表を照会できない。
5. PlatformAuditorは期間必須の専用経路で照会できる。
6. 通常監査RLSとplatform監査権限を同じpolicyへ混在させない。

## 変更2: TC-ACC-MVS01-071-API

公開429の合格条件へ次を追加する。

1. 同一partition・同一normalized action・同一UTC 1分窓で多数の429を発生させる。
2. 監査行は窓の先頭1件を上限とし、要求数に線形比例しない。
3. 2件目以降に対して`security_audit_suppressed_total`が増える。
4. 窓をまたいだ先頭事象は再び1件記録できる。
5. 抑制・監査sink障害でも元の429応答は変化しない。
6. 生IP、token、Cookie、CSRF値、外部組織IDをpartition値・ログ・監査へ保存しない。

## 変更3: V字追跡

上記2件の仕様根拠へADR-0010 D1/D2を追加する。ID、層別件数、優先度P0、既存試験期待値の変更統制は維持する。

## 実行状態

Stage 6R-1では両契約を赤で固定した。PostgreSQL実プロセス、HTTP大量429、role分離の挙動試験はT2/T3で行い、それまでは合格と数えない。

## Stage 6R-4C CI追補

PostgreSQL native試験の件数とIDは変更しない。CI構成検査6件は受入試験85件や補助TC-066-APIへ加算せず、test harnessの構成gateとして別管理する。DB受入は、非root runner、`executionMode=native`、`isSimulated=false`、API 36/36、PostgreSQL 10/10、終了コード0の全条件を満たすGitHub Actions証跡でだけ閉じる。

## Stage 6R-5 Draft PR受入追補

`TC-ACC-MVS01-072-API`をsource contractからnative API試験へ移し、Reviewer拒否、Auditor取得、tenant不可視、1〜200件上限、旧無制限経路廃止を検査する。API suiteは37件となる。

Draft PR受入gateはDomain 12、API 37、Mobile 6、OIDC 7、PostgreSQL 10、DR 4の合計76件をexact-countで要求する。CI構成検査8件は受入件数へ加算しない。DRは隔離local processのnative試験であり、石狩・東京の実リージョン切替を合格と表現しない。

## Stage 6R-6 Platform Security監査追補

`TC-ACC-MVS01-070-API`、`071-API`、`071-PG`、`080-API`をsource contractからnative試験へ移す。相関IDと要求IDの分離、PlatformAuditor期間必須API、429書込み抑制、sink障害時の元応答維持、application/writer/reader DB role分離を検査する。

API suiteは40件、PostgreSQL suiteは11件となる。Stage 6R-6 gateはDomain 12、API 40、Mobile 6、OIDC 7、PostgreSQL 11、DR 4の合計80件をexact-countで要求する。CI構成検査6件は受入件数へ加算しない。残存failure-first contractは6件である。

## Stage 6R-10 東京–石狩DR証跡追補

`TC-ACC-MVS01-078-DR`をsource contractからnative DR試験へ移す。旧primary停止、migration 005・複合外部キー・platform監査の復元、異subject二者承認、切替時系列、canonical JSONのSHA-256封印を一試験で検査する。

DR suiteは5件となる。Stage 6R-10 gateはDomain 12、API 41、Mobile 7、OIDC 8、PostgreSQL 12、DR 5の合計85件をexact-countで要求する。native local dual-cluster実行を物理的なさくら石狩・東京リージョン切替と表現しない。残存failure-first contractは性能1件である。
