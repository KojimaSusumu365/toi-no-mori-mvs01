# QF-UML-MVS01-006 Stage 6 業務フロー UML仕様書

- Version: 0.6
- 日付: 2026-08-16
- 対象: スマートフォン編集・審査・公開ワークスペース

以下のPlantUML原本を要求、実装、試験と同じ変更で維持する。

## UML-UC-MVS01-006 利用者と業務

```plantuml
@startuml
left to right direction
actor PublicUser as Public
actor Editor
actor Reviewer
rectangle "ToiNoMori mobile workspace" {
  usecase "Search published questions" as Search
  usecase "Create and edit own draft" as Edit
  usecase "Submit for review" as Submit
  usecase "Review queue" as Queue
  usecase "Return with reason" as Return
  usecase "Approve and publish" as Approve
  usecase "Withdraw publication" as Withdraw
  usecase "Read audit trail" as Audit
}
Public --> Search
Editor --> Edit
Editor --> Submit
Reviewer --> Queue
Reviewer --> Return
Reviewer --> Approve
Reviewer --> Withdraw
Reviewer --> Audit
note right of Approve
  OwnerSubject != ReviewerSubject
end note
@enduml
```

## UML-NAV-MVS01-006 画面遷移

```plantuml
@startuml
[*] --> PublicSearch
PublicSearch --> OidcLogin : administrator login
OidcLogin --> EditorWorkspace : Editor + MFA
OidcLogin --> ReviewerWorkspace : Reviewer + MFA
EditorWorkspace --> DraftEdit : create/edit
DraftEdit --> EditorWorkspace : save
EditorWorkspace --> ReviewerWorkspace : submit / role switch
ReviewerWorkspace --> EditorWorkspace : return with reason
ReviewerWorkspace --> PublicSearch : approve and publish
ReviewerWorkspace --> AuditView : audit tab
AuditView --> ReviewerWorkspace : review tab
EditorWorkspace --> PublicSearch : logout
ReviewerWorkspace --> PublicSearch : logout
@enduml
```

## UML-SEQ-MVS01-015 OIDC二利用者の公開完結（TC-052、057）

```plantuml
@startuml
actor Editor
participant "Editor browser" as EB
participant "OIDC Provider" as IdP
participant "BFF / Admin API" as API
database "Question Store" as Store
participant "Reviewer browser" as RB
actor PublicUser as Public

Editor -> EB : login with MFA
EB -> IdP : code + PKCE authorization
IdP --> API : signed Editor identity
API --> EB : Secure HttpOnly Cookie
EB -> API : POST draft + CSRF
API -> Store : create(owner=Editor, DRAFT)
Store --> API : version=1
API --> EB : 201 + ETag
EB -> API : PUT draft + CSRF + If-Match
API -> Store : update only owner and matching version
EB -> API : POST submit + CSRF
API -> Store : DRAFT -> IN_REVIEW

RB -> IdP : separate Reviewer login + MFA
IdP --> API : signed Reviewer identity
API --> RB : separate Secure HttpOnly Cookie
RB -> API : GET admin?status=IN_REVIEW
API -> Store : reviewer list
Store --> RB : review queue
RB -> API : POST approve + CSRF + Idempotency-Key
API -> Store : reject self approval; publish atomically
Store --> RB : PUBLISHED
Public -> API : GET public question
API -> Store : published only
Store --> Public : public DTO
@enduml
```

## UML-SEQ-MVS01-016 差し戻しと再編集（TC-053）

```plantuml
@startuml
actor Reviewer
participant ReviewerUI
participant AdminAPI
database Store
participant EditorUI
actor Editor

Reviewer -> ReviewerUI : enter return reason
ReviewerUI -> AdminAPI : POST return + CSRF + reason
AdminAPI -> Store : IN_REVIEW -> DRAFT; version++
Store --> ReviewerUI : DRAFT + ReviewReason
Editor -> EditorUI : open own list
EditorUI -> AdminAPI : GET administrative list
AdminAPI -> Store : owner_subject = Editor sub
Store --> EditorUI : DRAFT + ReviewReason + version
Editor -> EditorUI : correct content
EditorUI -> AdminAPI : PUT + CSRF + If-Match
AdminAPI -> Store : update; clear handled reason; version++
Store --> EditorUI : corrected DRAFT
@enduml
```

## UML-SEC-MVS01-006 管理閲覧境界

```plantuml
@startuml
start
:GET administrative question(s);
if (Authenticated?) then (no)
  :401;
  stop
endif
if (MFA evidence?) then (no)
  :403;
  stop
endif
if (Reviewer role?) then (yes)
  :Allow review scope;
else (no)
  if (Editor role?) then (yes)
    :Constrain query by owner_subject = sub;
  else (no)
    :403;
    stop
  endif
endif
if (Accessible record exists?) then (yes)
  :Return admin DTO;
else (no)
  :404 without enumeration;
endif
stop
@enduml
```

## UML-TST-MVS01-006 V字対応

```plantuml
@startuml
left to right direction
rectangle "REQ-UI-001/002\nrole workspaces" as Requirements1
rectangle "REQ-UI-003/004\nworkflow and return" as Requirements2
rectangle "REQ-SEC-006\nadmin read boundary" as Requirements3
rectangle "UML-UC/NAV/SEQ/SEC-006" as Design
rectangle "Admin GET API + mobile UI" as Code
rectangle "API TC-049..054/059" as ApiTests
rectangle "Mobile TC-055/056" as UiTests
rectangle "OIDC E2E TC-057" as OidcTest
rectangle "PostgreSQL TC-058" as DbTest

Requirements1 --> Design
Requirements2 --> Design
Requirements3 --> Design
Design --> Code
Code --> ApiTests
Code --> UiTests
Code --> OidcTest
Code --> DbTest
ApiTests --> Requirements1 : verifies
ApiTests --> Requirements2 : verifies
UiTests --> Requirements3 : verifies
OidcTest --> Requirements2 : verifies with two signed sessions
DbTest --> Requirements3 : verifies SQL row scope
@enduml
```

Stage 6ローカル主要層の完了条件はDomain 9、API 32、Mobile 6、OIDC E2E 7の計54件を同一版で合格させること。PostgreSQL TC-024〜027/058とDR TC-030〜033を含む全層63件、実browser engine、実IdP、実クラウドは別gateとする。
