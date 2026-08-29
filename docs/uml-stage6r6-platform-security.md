# Stage 6R-6 Platform Security監査 UML仕様書

- 文書ID: QF-UML-MVS01-6R6
- 版: Version 0.1
- 対応仕様: QF-ST6R6-MVS01-001

## Component diagram

```mermaid
flowchart TB
    Client["Client / browser"] --> Pipeline["Correlation + security middleware"]
    Pipeline --> Api["Business / platform API"]
    Pipeline --> Queue["Bounded audit queue"]
    Queue --> Writer["Writer-only sink"]
    Writer --> Table["platform_security_events"]
    PlatformAuditor["PlatformAuditor"] --> Reader["Reader-only API"]
    Reader --> Table
```

tenant Auditorは`audit_events`だけをtenant RLS経由で読み、上図のReaderへ入れない。

## Sequence diagram — 429抑制

```mermaid
sequenceDiagram
    participant C as Client
    participant M as Middleware
    participant Q as Audit queue
    participant W as Writer
    participant D as PostgreSQL
    C->>M: public request
    M-->>C: 429 + request/correlation IDs
    M->>Q: first event only
    Q->>W: async write with timeout
    W->>D: INSERT allowlisted metadata
    C->>M: repeated request in same minute
    M-->>C: 429 unchanged
    M->>M: increment suppressed metric
```

## Deployment / credential boundary

```mermaid
flowchart LR
    App["Application role"] --> TenantDB["tenant business tables"]
    Migrator["Migration role"] --> Schema["schema + grants"]
    WriterRole["Platform writer role"] --> PlatformDB["platform events"]
    ReaderRole["Platform reader role"] --> PlatformDB
```

application roleからPlatformDBへの経路、およびwriter roleからSELECTへの経路は存在しない。

## V字対応

```mermaid
flowchart TB
    S1["ID分離仕様"] --> I1["Correlation middleware"]
    S2["監査・抑制仕様"] --> I2["Queue / sink / migration"]
    I1 --> T1["API TC-070"]
    I2 --> T2["API TC-071 / 080・PG TC-071"]
    T1 --> A["Stage 6R-6 80件gate"]
    T2 --> A
```
