# Stage 6R-8 スマートフォン承認ETag・role別DTO UML仕様書

- 文書ID: QF-UML-MVS01-6R8
- 版: Version 0.1
- 日付: 2026-08-25
- 対応仕様: QF-ST6R8-MVS01-001

## UML-SEQ-MVS01-6R8-001 審査済みETagによる承認

```mermaid
sequenceDiagram
    actor Reviewer
    participant Mobile
    participant API
    participant Store
    Mobile->>API: GET administrative detail
    API->>Store: visible snapshot
    Store-->>API: version N
    API-->>Mobile: Reviewer DTO + ETag N
    Reviewer->>Mobile: 内容を審査して承認
    Mobile->>API: POST approve / If-Match N
    API->>Store: Approve(expectedVersion=N)
    Store-->>API: published version N+1
    API-->>Mobile: 200 + ETag N+1
```

## UML-SEQ-MVS01-6R8-002 競合時の再審査

```mermaid
sequenceDiagram
    actor Reviewer
    participant Mobile
    participant API
    Reviewer->>Mobile: 古い詳細を承認
    Mobile->>API: POST approve / If-Match N
    API-->>Mobile: 409 Conflict
    Mobile->>Mobile: approvalEtagを破棄
    Mobile-->>Reviewer: 自動再送せず再読込・再審査を要求
```

## UML-CMP-MVS01-6R8-001 role別DTO境界

```mermaid
flowchart TD
    Snapshot["QuestionSnapshot"] --> EditorMap["Editor mapper"]
    Snapshot --> ReviewerMap["Reviewer mapper"]
    Snapshot --> PublicMap["Public mapper"]
    EditorMap --> EditorDTO["Editor DTO: review reason"]
    ReviewerMap --> ReviewerDTO["Reviewer DTO: owner and both reasons"]
    PublicMap --> PublicDTO["Public DTO: published fields only"]
```

## UML-TST-MVS01-6R8-001 V字対応

| 左側設計 | 右側試験 |
|---|---|
| 詳細ETag保持・ETagなし承認不可 | TC-ACC-MVS01-076-MOB |
| 409後の自動再送禁止・再審査 | TC-ACC-MVS01-076-MOB |
| Editor DTOのwithdrawalReason非開示 | TC-ACC-MVS01-081-API |
| Reviewer DTOのowner・withdrawalReason許可 | TC-ACC-MVS01-081-API |
| Public DTOの理由非開示 | TC-ACC-MVS01-081-API |
| exact-count全体回帰 | Stage 6R-8非root native 83/83 gate |
