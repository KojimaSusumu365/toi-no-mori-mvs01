# Stage 6R-4 テナント境界 UML仕様書

- 文書ID: QF-UML-MVS01-6R4
- 版: Version 0.3
- 対応: ADR-0007 D1〜D4、ADR-0008 D5、TC-065〜069/074/075

## コンポーネント

```plantuml
@startuml
actor "Editor / Reviewer" as User
component "OIDC/BFF\nverified principal" as BFF
component "RequireTenantFilter" as Filter
component "TenantResolver\nexternal allow-list" as Resolver
component "Admin endpoints" as API
component "IQuestionStore" as Store
database "PostgreSQL\nRLS FORCE" as PG
component "Application DB role\nnon-owner / no BYPASSRLS" as AppRole
component "Migration DB role\nDDL owner" as MigrationRole

User --> BFF
BFF --> Filter : iss, sub, roles, amr,\nexternal_organization_id
Filter --> Resolver : verified principal
Resolver --> Filter : internal tenant UUID
Filter --> API : HttpContext internal_tenant_id
API --> Store : tenantId + command/query
Store --> AppRole : application credential
AppRole --> PG : BEGIN + set_config(local) + SQL
MigrationRole --> PG : Migration 001-003
PG --> Store : tenant RLS result
note bottom of AppRole
  Required by ADR-0007 / TC-066-PG.
  Role split and startup diagnostics are
  implemented; real DB execution is pending.
end note
@enduml
```

## tenant解決と可視性

```plantuml
@startuml
actor Client
participant "AuthN/AuthZ" as Auth
participant "Tenant filter" as Filter
participant "Allow-list resolver" as Resolver
participant "Endpoint" as API
participant "Question store" as Store
database "PostgreSQL RLS" as DB

Client -> Auth : management request
Auth -> Filter : verified principal
Filter -> Resolver : iss + external_organization_id
alt claim missing
  Resolver --> Filter : tenant.claim_missing
  Filter --> Client : 403 stable problem code
else duplicate/unmapped
  Resolver --> Filter : tenant.claim_invalid_or_unmapped
  Filter --> Client : 403 without reflected value
else mapped
  Resolver --> Filter : internal tenant UUID
  Filter -> API : request + tenant context
  API -> Store : tenantId + actor + resourceId
  Store -> DB : BEGIN
  Store -> DB : set_config(app.tenant_id, tenantId, true)
  Store -> DB : tenant-bound SQL
  alt visible in same tenant and owner/role
    DB --> Client : result
  else other tenant / other owner / missing
    DB --> Store : no visible aggregate or ownership rejection
    Store --> Client : identical normalized Problem Details 404
  end
end
@enduml
```

## データ制約

```mermaid
erDiagram
    QUESTIONS ||--o{ QUESTION_REVISIONS : "tenant_id + question_id"
    QUESTIONS {
        uuid tenant_id
        uuid id
        uuid published_revision_id
        int approved_version
    }
    QUESTION_REVISIONS {
        uuid tenant_id
        uuid question_id
        uuid id
        int version
    }
    IDEMPOTENCY_RECORDS {
        uuid tenant_id
        string idempotency_key
        string actor_subject
        int expected_version
        timestamp expires_at
    }
    AUDIT_EVENTS {
        uuid tenant_id
        uuid target_id
        string action
    }
```

`published_revision_id`はrevision ID単独で結ばない。`(tenant_id, question.id, published_revision_id)`から`(tenant_id, question_revisions.question_id, question_revisions.id)`への複合FKで、tenant越境と別question参照を同時に拒否する。

## V字対応

```plantuml
@startuml
left to right direction
rectangle "ADR-0007 D2\nclaim allow-list" as SpecA
rectangle "ADR-0007 D1/D3/D4\nRLS + composite FK" as SpecB
rectangle "TenantResolver / Store /\nMigration 002-003" as Impl
rectangle "TC-065/069-API\nclaim + normalized 404" as ApiTest
rectangle "TC-066/067/068/074/075-PG\nreal DB" as PgTest
SpecA --> Impl
SpecB --> Impl
Impl --> ApiTest
Impl --> PgTest
@enduml
```

API試験の緑はDB試験の代替ではない。application/migration DBロール分離と起動時診断は実装済みだが、PostgreSQL側5件は実DB未実行のため右側V字は未閉鎖である。

## DBロール起動診断

```plantuml
@startuml
start
:Production configurationを読む;
if (application/migration接続が両方ある?) then (yes)
  if (usernameが異なり双方VerifyFull?) then (yes)
    :migration roleで001-003適用;
    :application roleへ最小DMLをGRANT;
    :application roleでcatalog診断;
    if (NOINHERIT and non-owner\nand !superuser and !BYPASSRLS\nand no schema CREATE?) then (yes)
      :API起動継続;
    else (no)
      :安全側に起動失敗;
    endif
  else (no)
    :構成時点で起動失敗;
  endif
else (no)
  :構成時点で起動失敗;
endif
stop
@enduml
```

## 非root CI受入境界

```mermaid
flowchart TD
    A["GitHub Actions / Ubuntu 24.04"] --> B{"非root UID"}
    B -- No --> F["Rejected evidence"]
    B -- Yes --> C["API 36件"]
    C --> D["一時PostgreSQL 18.6 / 分離4ロール"]
    D --> E["PostgreSQL 10件"]
    E --> G{"36/36 AND 10/10"}
    G -- No --> F
    G -- Yes --> H["Accepted evidence + log SHA-256"]
```

CI構成試験6件はworkflowと証跡判定器の契約を検査するが、右側V字を閉じるのは非root runner上のnative PostgreSQL 10/10だけである。root、模擬実行、件数不足、gate非0の証跡はすべて`rejected`となる。
