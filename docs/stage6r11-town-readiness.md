# Stage 6R-11 — Question Forest Minimum / Town Readiness Check

## 1. 目的

Stage 6R-11 は Virtual Town を実装する段階ではない。

既存の Question Forest Minimum が、将来 Virtual Town を別 Domain として横付けするときに CORE の再設計を必要としないかを、V字の仕様・試験対で確認する。

最上位の責任分離は次の通りとする。

- Question Forest: 問いを保持し、Evidence・公開情報へ接続する上流 Domain
- Virtual Town: 問いから生まれた Task を人が引き受ける下流 Domain
- Question は Forest の Entity のまま維持する
- 将来の Town は Question を `context_ref` 相当の外部参照として扱い、Question 本文を所有しない

本 Stage では Town runtime、Town DB、Experience Ledger、Integration Gateway を追加しない。

## 2. Readiness requirement

### REQ-QF-TR-001 — Stable Question Reference

Question は Draft → InReview → Published → Withdrawn の lifecycle を通じて同一 `Guid` を維持し、公開 projection でも同じ ID を返すこと。

Acceptance test: `TC-ACC-MVS01-082-TR`

### REQ-QF-TR-002 — Public Read Boundary

`/api/public/questions` は `Published` の Question だけを返すこと。

`Draft` / `InReview` / `Withdrawn` は detail と search の双方から取得できないこと。

Acceptance test: `TC-ACC-MVS01-083-TR`

### REQ-QF-TR-003 — Non-public Data Leakage Prevention

Public DTO は allowlist とし、次のみを返すこと。

- `id`
- `title`
- `body`
- `tags`
- `publishedAt`

少なくとも `ownerSubject` / `reviewReason` / `withdrawalReason` / `tenantId` / `approvedBy` を返してはならない。

Acceptance test: `TC-ACC-MVS01-084-TR`

### REQ-QF-TR-004 — Domain Role Independence

将来の Town role を表す未知 role `TownAdmin` を提示しても、Forest の `Staff` / `Editor` / `Reviewer` 権限へ自動昇格してはならない。

Public Read は role-neutral であり、公開済み Question は Town identity からでも公開情報として読めること。

Acceptance test: `TC-ACC-MVS01-085-TR`

### REQ-QF-TR-005 — Unpublish Lifecycle

公開済み Question を Withdrawn にした後、同じ安定 ID で Public API から本文を再取得できてはならない。

一方、行政・監査 lifecycle record は同じ ID のまま Forest 内部に保持されること。

Stage 6R-11 では物理 DELETE や Town 側 tombstone を実装しない。将来 Town が存在するときの reference lifecycle は VT 側 Stage で扱う。

Acceptance test: `TC-ACC-MVS01-086-TR`

### REQ-QF-TR-006 — Full Regression

Stage 6R-10 までの Domain / API / Mobile / OIDC / PostgreSQL / DR 回帰を維持し、上記 Town-readiness 5件を追加した全 90 件が GREEN であること。

CI gate: `.github/workflows/stage6r11-town-readiness.yml`

## 3. 実装判定

上記5件が既存 Question Forest の挙動だけで GREEN となり、全90件の回帰も GREEN の場合、次を判定する。

> Virtual Town 対応のための Question Forest CORE 変更は Stage 6R-11 時点では不要。

必要なのは将来の Adapter / Integration 境界であり、Question Forest CORE への Town 機能追加ではない。

## 4. 非対象

Stage 6R-11 では次を実装しない。

- Virtual Town runtime
- Town Task / Project table
- Town role store
- Experience Ledger
- AI Customer / AI Resident
- Citizen Compute
- Integration Gateway runtime
- Town cache / tombstone runtime
- 3D world

## 5. PASS criteria

- [ ] `TC-ACC-MVS01-082-TR` GREEN
- [ ] `TC-ACC-MVS01-083-TR` GREEN
- [ ] `TC-ACC-MVS01-084-TR` GREEN
- [ ] `TC-ACC-MVS01-085-TR` GREEN
- [ ] `TC-ACC-MVS01-086-TR` GREEN
- [ ] Domain 12/12 GREEN
- [ ] API 41/41 GREEN
- [ ] Mobile 7/7 GREEN
- [ ] OIDC E2E 8/8 GREEN
- [ ] Town readiness 5/5 GREEN
- [ ] PostgreSQL 12/12 GREEN
- [ ] DR 5/5 GREEN
- [ ] total 90/90 GREEN
- [ ] test ID uniqueness GREEN
- [ ] Release build warning 0 / error 0
- [ ] non-root native CI gate GREEN

## 6. 次段階

PASS 後は Stage 6R-12 — Question Forest Minimum v1 Release Candidate へ進む。

Stage 6R-12 の目的は Town 機能追加ではなく、Question 登録 → Review → Published → Public Read という Question Forest Minimum の一本を Release Candidate として固定することである。
