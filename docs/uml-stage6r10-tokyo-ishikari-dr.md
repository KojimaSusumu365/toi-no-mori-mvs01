# Stage 6R-10 東京–石狩DR切替・復旧証跡 UML仕様書

- 文書ID: QF-UML-MVS01-6R10
- 版: Version 0.1
- 日付: 2026-08-25
- 対応仕様: QF-ST6R10-MVS01-001

## UML-DPL-MVS01-6R10-001 native二重クラスタ

```mermaid
flowchart TD
    Primary["石狩primary役 PostgreSQL"] --> Backup["署名・AES-256-GCM backup"]
    Backup --> Recovery["東京recovery役 空DB"]
    Recovery --> Accept["API・監査・schema受入"]
    Accept --> Route["論理routeを東京へ切替"]
    Primary --> Isolation["write停止確認"]
    Isolation --> Route
```

これはローカルで別data directoryと別portを使うnative配置であり、物理的な石狩・東京リージョンではない。

## UML-SEQ-MVS01-6R10-001 切替・証跡封印

```mermaid
sequenceDiagram
    actor IC as Incident Commander
    actor RL as Recovery Lead
    participant I as 石狩primary役
    participant R as 東京recovery役
    participant V as Evidence Validator
    IC->>I: 災害宣言・write停止
    I-->>V: stopped=true
    V->>R: 暗号化backupを隔離復元
    R-->>V: API・監査・schema結果
    IC->>V: recovery承認
    RL->>V: route切替承認
    V->>V: subject分離・順序・安全条件
    V->>V: canonical JSON + SHA-256
    V-->>R: 東京route accepted
```

## UML-SM-MVS01-6R10-001 DR状態

```mermaid
stateDiagram-v2
    [*] --> IshikariPrimary
    IshikariPrimary --> DisasterDeclared
    DisasterDeclared --> SourceIsolated
    SourceIsolated --> TokyoRestoring
    TokyoRestoring --> RecoveryAccepted
    RecoveryAccepted --> TokyoPrimary
    DisasterDeclared --> Rejected: source未隔離
    TokyoRestoring --> Rejected: schema不整合
    RecoveryAccepted --> Rejected: 二者承認不成立
```

## UML-TST-MVS01-6R10-001 V字対応

| 左側設計 | 右側試験 |
|---|---|
| source write先行隔離 | TC-ACC-MVS01-078-DR |
| 異subject二者承認 | TC-ACC-MVS01-078-DR |
| migration 005・複合FK・platform監査復元 | TC-ACC-MVS01-078-DR |
| 切替時系列fail-closed | TC-ACC-MVS01-078-DR |
| canonical artifact SHA-256封印 | TC-ACC-MVS01-078-DR |
| exact-count全体回帰 | Stage 6R-10非root native 85/85 gate |
