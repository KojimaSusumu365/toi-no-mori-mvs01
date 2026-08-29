# Stage 6R-9 実OIDC tenant mapping・自己承認境界 UML仕様書

- 文書ID: QF-UML-MVS01-6R9
- 版: Version 0.1
- 日付: 2026-08-25
- 対応仕様: QF-ST6R9-MVS01-001

## UML-SEQ-MVS01-6R9-001 OIDC tenant mapping

```mermaid
sequenceDiagram
    actor User
    participant Browser
    participant BFF
    participant IdP
    participant TenantMap
    User->>Browser: login
    Browser->>BFF: authorization callback
    BFF->>IdP: code + PKCE token交換
    IdP-->>BFF: signed ID token + external org
    BFF->>TenantMap: verified issuer + external org
    TenantMap-->>BFF: internal tenant UUID
    BFF-->>Browser: internal tenant付きHttpOnly Cookie
```

未登録組織では`TenantMap`が拒否し、BFFは汎用failureへredirectしてCookieを発行しない。

## UML-SEQ-MVS01-6R9-002 dual-role自己承認拒否

```mermaid
sequenceDiagram
    actor Owner
    participant Browser
    participant API
    participant Domain
    Owner->>Browser: Editor + Reviewer login
    Browser->>API: 作成・申請・詳細GET
    API-->>Browser: detail + strong ETag N
    Browser->>API: approve / If-Match N
    API->>Domain: Approve(owner sub, N)
    Domain-->>API: self forbidden
    API-->>Browser: 403
```

## UML-CMP-MVS01-6R9-001 claim境界

```mermaid
flowchart TD
    Token["署名検証済みID token"] --> Clean["予約claim除去"]
    Clean --> Map["issuer付き組織許可表"]
    Map --> Internal["サーバー生成internal tenant claim"]
    Internal --> Cookie["HttpOnly BFF Cookie"]
    Map --> Reject["未登録: session拒否"]
```

## UML-TST-MVS01-6R9-001 V字対応

| 左側設計 | 右側試験 |
|---|---|
| 署名済み外部組織のissuer付き変換 | TC-ACC-MVS01-077-OIDC |
| 未登録組織のCookie発行前拒否 | TC-ACC-MVS01-077-OIDC |
| 内部tenantへの保存確認 | TC-ACC-MVS01-077-OIDC |
| dual-role strong ETag自己承認403 | TC-ACC-MVS01-077-OIDC |
| 同一tenant・異subject承認200 | TC-ACC-MVS01-077-OIDC |
| exact-count全体回帰 | Stage 6R-9非root native 84/84 gate |
