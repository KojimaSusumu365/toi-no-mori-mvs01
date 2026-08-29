# QF-UML-MVS01-003 災害復旧 UML仕様書

- Version: 0.3
- 日付: 2026-08-16
- 対象: MVS-01 Stage 3

以下は実装、要求、試験と同じ変更で維持するPlantUML原本である。`implemented` は本リポジトリで自動検証済み、`planned cloud` はさくら実環境での構築・訓練待ちを表す。

## UML-DPL-MVS01-003 石狩本番・東京復旧配置

```plantuml
@startuml
left to right direction
skinparam componentStyle rectangle

actor "Smartphone / Browser" as Client
cloud "Sakura Global\nGSLB\n(planned cloud)" as Gslb

node "Ishikari region - production" as Ishikari {
  node "API tier\n2 instances target" as IshikariApi
  database "PostgreSQL primary\n+ local HA target" as IshikariDb
  component "Backup worker\nimplemented script" as Backup
  component "Signing private key\nsecret store" as SignKey
}

cloud "Ishikari Object Storage\nversioned source bucket\n(planned cloud)" as IshikariObject
cloud "Tokyo Object Storage\nversioned destination bucket\n(planned cloud)" as TokyoObject

node "Tokyo region - cold recovery" as Tokyo {
  component "Recovery control\nimplemented script" as Recovery
  component "Recovery private key\nTokyo only" as RecoveryKey
  component "Pinned signer certificate" as Trust
  database "Isolated PostgreSQL\nseparate data directory" as TokyoDb
  node "Recovery API" as TokyoApi
}

Client --> Gslb : HTTPS
Gslb --> IshikariApi : normal route
Gslb ..> TokyoApi : manual DR route
IshikariApi --> IshikariDb : PostgreSQL TLS VerifyFull
Backup --> IshikariDb : pg_dump read
Backup --> SignKey : CAdES sign
Backup --> IshikariObject : AES-256-GCM ciphertext only
IshikariObject ..> TokyoObject : async CRR
TokyoObject --> Recovery : selected version
Recovery --> RecoveryKey : decrypt
Recovery --> Trust : verify signer
Recovery --> TokyoDb : pg_restore
TokyoApi --> TokyoDb : PostgreSQL TLS VerifyFull
@enduml
```

配置制約:

- 復旧秘密鍵を石狩へ配置しない。
- DB portをInternetへ公開しない。APIまたは限定したbackup/recovery主体だけを許可する。
- 石狩と東京を同時にwrite primaryにしない。
- CRR到達確認前のbackupを「東京復旧可能」と記録しない。
- GSLB切替は旧石狩write経路の隔離と二者承認後に行う。

## UML-SEQ-MVS01-007 暗号化バックアップ（TC-030）

```plantuml
@startuml
actor Scheduler
participant "Backup worker" as Backup
database "Ishikari PostgreSQL" as PrimaryDb
participant "OpenSSL CMS" as Crypto
collections "Ishikari bucket" as IshikariBucket
collections "Tokyo bucket" as TokyoBucket
participant Monitor

Scheduler -> Backup : start hourly backup
Backup -> PrimaryDb : pg_dump custom format
PrimaryDb --> Backup : consistent logical dump
Backup -> Backup : SHA-256 internal manifest
Backup -> Crypto : CAdES sign with signer key
Crypto --> Backup : signed CMS
Backup -> Crypto : AES-256-GCM encrypt to recovery certificate
Crypto --> Backup : authenticated ciphertext
Backup -> IshikariBucket : put ciphertext and metadata
IshikariBucket -> TokyoBucket : asynchronous CRR
Backup -> IshikariBucket : head ReplicationStatus
Backup -> TokyoBucket : head size and metadata SHA-256
Backup --> Monitor : verified object key or failure
@enduml
```

事後条件:

- 確定objectは`.p7m`暗号文であり、DB本文のsentinelを平文検索できない。
- 内部manifestとdumpは暗号化範囲内にあり、metadataだけでは復元を信頼しない。
- CRRは非同期なので、生成完了、石狩upload、東京到達を別々に監視する。

## UML-SEQ-MVS01-008 改ざん拒否（TC-031）

```plantuml
@startuml
actor RecoveryOperator
participant "Recovery script" as Recovery
participant "OpenSSL CMS" as Crypto
database "Empty recovery PostgreSQL" as RecoveryDb

RecoveryOperator -> Recovery : select ciphertext with one byte changed
Recovery -> Crypto : decrypt and authenticate GCM
Crypto --> Recovery : verification error
Recovery --> RecoveryOperator : nonzero exit
Recovery -[#red]x RecoveryDb : pg_restore must not start
@enduml
```

GCM認証を通過しても、CAdES署名、固定した署名者証明書、internal dump SHA-256を順に検証し、どれか一つでも失敗した時点でDB操作前に終了する。

## UML-SEQ-MVS01-009 隔離復元とRPO/RTO（TC-032、TC-033）

```plantuml
@startuml
actor "Incident Commander" as IC
participant "Ishikari API" as PrimaryApi
database "Ishikari PostgreSQL" as PrimaryDb
collections "Tokyo bucket" as TokyoBucket
participant "Recovery control" as Recovery
database "Tokyo isolated PostgreSQL" as RecoveryDb
participant "Tokyo API" as RecoveryApi
participant "GSLB" as Gslb
actor Validator

IC -> PrimaryApi : block writes
IC -> PrimaryDb : isolate or stop
IC -> IC : record disaster declaration UTC
IC -> Recovery : approve selected replicated backup
Recovery -> TokyoBucket : get ciphertext version
TokyoBucket --> Recovery : encrypted backup
Recovery -> Recovery : decrypt, CAdES verify, SHA-256 verify
Recovery -> RecoveryDb : pg_restore into empty database
Recovery -> RecoveryApi : start with recovery DB
Validator -> RecoveryApi : health and public sentinel GET
RecoveryApi -> RecoveryDb : read published question
RecoveryDb --> RecoveryApi : question
Validator -> RecoveryDb : verify audit and migrations
Recovery -> Recovery : calculate RPO and RTO
Recovery --> IC : integrity report and metrics
IC -> Gslb : two-person approved Tokyo route
Gslb --> RecoveryApi : public HTTPS traffic
@enduml
```

RPOは`disasterDeclaredAt - snapshotStartedAt`、RTOは`recoveryAcceptedAt - disasterDeclaredAt`とする。ローカル自動試験ではGSLB切替を実行せず、API/DB整合性確認時点までをRTOとして計測する。実クラウド訓練ではGSLB切替と外部smoke test完了までを含める。

## UML-TST-MVS01-003 V字対応

```plantuml
@startuml
left to right direction
rectangle "REQ-MVS01-DR-001\nTokyo/Ishikari separation" as Req1
rectangle "REQ-MVS01-DR-002\nConfidential backup" as Req2
rectangle "REQ-MVS01-DR-003\nTamper rejection" as Req3
rectangle "REQ-MVS01-DR-004\nIsolated restore" as Req4
rectangle "REQ-MVS01-DR-005\nRPO/RTO measurement" as Req5
rectangle "ADR-0003\nUML-DPL/SEQ" as Design
rectangle "Backup/restore scripts" as Code
rectangle "TC-030" as Tc30
rectangle "TC-031" as Tc31
rectangle "TC-032" as Tc32
rectangle "TC-033" as Tc33

Req1 --> Design
Req2 --> Design
Req3 --> Design
Req4 --> Design
Req5 --> Design
Design --> Code
Code --> Tc30
Code --> Tc31
Code --> Tc32
Code --> Tc33
Tc30 --> Req2 : verifies
Tc31 --> Req3 : verifies
Tc32 --> Req4 : verifies
Tc33 --> Req5 : verifies
@enduml
```

Stage 3時点の完了条件は、要求ID、ADR、UML ID、script、TC-030〜033、復旧証跡の対応が切れていないこと、かつ当時のローカル全37件が合格することだった。Stage 5追加後の現行gateは`uml-stage5.md`と`verification-result.md`に記録した全52件である。クラウド運用開始条件はこれに加え、実バケットCRRと東京復旧訓練の合格を必要とする。
