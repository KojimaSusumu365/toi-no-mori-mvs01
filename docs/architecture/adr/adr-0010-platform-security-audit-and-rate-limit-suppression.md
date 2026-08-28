# ADR-0010 テナント未確定拒否の監査分離と429書込み抑制

- 版: Version 0.1
- 状態: 承認済み（2026-08-20、Stage 6R-1着手承認に基づく補足決定）
- 関連: ADR-0007 D5、ADR-0009 D4/D8、RVR-N01、RVR-N02
- Owner: Security Reviewer / System Architect / Operations

## 文脈

外部レビューは、`tenant_id IS NULL`の拒否イベントをtenant RLS下の`audit_events`へ置くと、書込みはできても通常のtenant監査経路から読めないことを実測した。また、公開APIの429を1要求1行で記録すると、レート制限攻撃が監査DBへの書込み増幅になる。

## 決定

### D1. テナント境界の外側を専用監査流へ分離する

- `audit_events.tenant_id`は`NOT NULL`とし、業務・tenant内拒否だけを保存する。
- tenantを確定できない`access.unauthenticated`、`tenant.claim_missing`、`tenant.claim_invalid_or_unmapped`は、別表`platform_security_events`へ保存する。
- `platform_security_events`は通常tenant接続・Auditor APIから照会できない。期間必須のPlatformAuditor経路だけが照会する。
- PlatformAuditorとtenant Auditorは兼務させず、migration role、application role、platform audit writer/readerを分ける。
- イベント本文、token、Cookie、CSRF値、claim全文、外部組織IDを保存しない。

### D2. 公開429を1分窓で抑制する

- 同一の`partition_hash + normalized_action + UTC 1-minute window`につき、先頭1件だけを監査行へ保存する。
- 2件目以降は同期INSERTせず、`security_audit_suppressed_total`メトリクスを増やす。
- 窓終了時の周期サマリーに`occurrence_count`を出してよいが、要求数と監査行数を線形比例させない。
- `partition_hash`は生IPや認証情報を保存せず、rotation可能な秘密付きHMACなど不可逆な値とする。秘密が利用不能なら、より粗いサーバー側partitionへ安全側縮退する。
- 抑制器の障害・遅延で元の429応答を変更しない。失敗メトリクスと秘密値なし代替ログを残す。

## 却下した案

- `audit_events`のRLSへ`tenant_id IS NULL AND role=auditor`を加える案: tenant境界とplatform境界が同居し、将来のポリシー変更リスクが高いため却下。
- 全429を追記する案: 可用性攻撃を増幅するため却下。
- 429を完全に捨てる案: 調査の先頭証跡が失われるため却下。

## 受入試験

- `TC-ACC-MVS01-071-PG`: 通常監査のtenant必須、専用表・専用権限・許可reasonだけを検査する。
- `TC-ACC-MVS01-071-API`: 大量429に対し監査行が線形増加せず、抑制メトリクスが増えることを検査する。
- `TC-ACC-MVS01-080-API`: sink障害・遅延でも元の拒否応答が変わらないことを検査する。

## 再評価条件

監査基盤を外部SIEMへ移す、保持期間を変更する、複数公開tenantを開始する、またはPlatformAuditor運用を変更する場合、本ADRを再評価する。
