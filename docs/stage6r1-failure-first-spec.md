# Stage 6R-1 失敗先行テスト仕様・実施記録

- 文書ID: QF-ST6R1-MVS01-001
- 版: Version 0.1
- 日付: 2026-08-20
- 対象基準線: Stage 6 / v0.6
- 判定: **T1完了候補。製品実装着手は次gateで行う**

## 1. 承認された入力

| 入力 | SHA-256 | 扱い |
|---|---|---|
| Stage 6 / v0.6 ZIP | `acf52cd4af776a9ae137e159bb82469f74f4499520414d8aba297375e991f67d` | 不変の比較基準 |
| Stage 6R受入・ADR・試験計画ZIP | `3fc42dcb64b181b1008b2e133be91eb8ee76db704f9b1390437aeaad344670a8` | 承認済み設計基準 |
| 外部レビュー応答 QF-RVR-MVS01-001 | `31757d1670f27efd6a1d1938694bf16dded2350f1de40f1b66a36eae89bb8163` | 実測所見を受領 |

ADR-0007 v0.2、ADR-0008 v0.2、ADR-0009 v0.2はユーザー承認済みとする。RVR-N01/N02はADR-0010で補足した。過去のレビュー同梱コードは引き続き直接適用しない。

## 2. T1で変更した範囲

- 製品コード・DB migration・Web UI製品コードは変更していない。
- 新規22証跡を、実行可能な失敗先行contractとして`tests/stage6r1/stage6r1_red_tests.py`へ登録した。
- `scripts/test-stage6r1-red.sh`は22件がすべて期待理由で赤である時だけ、T1確認コマンドとして成功する。
- 通常のacceptance実行では赤を成功扱いしない。`--assert-red`はT1専用であり、T2以後の合格判定には使わない。
- 既存API試験ID重複を修正し、各C# suiteのrunnerとCI検査へ重複拒否を追加した。
- 承認済み範囲に従い、既存TC-055の監査画面期待をReviewerからAuditorへ変更した。他の既存期待値は変更していない。

## 3. 新規22契約

| 層 | ID | 赤の根拠 |
|---|---|---|
| Domain | 063-DOM | 承認If-Match、承認版・承認者が未実装 |
| API | 064-API | 承認endpointがIf-Matchを読まない |
| API | 065-API | 外部組織IDの許可表変換がない |
| PostgreSQL | 066-PG | tenant列・RLS・transaction GUCがない |
| PostgreSQL | 067-PG | `NULLIF(current_setting(...),'')`の4 policyがない |
| PostgreSQL | 068-PG | 同一tenant・同一questionの複合FKがない |
| API | 069-API | 他Editorが403で、秘匿404へ正規化されない |
| API | 070-API | correlation IDとclient request IDが分離されない |
| API | 071-API | 拒否監査envelope・reason・429抑制がない |
| PostgreSQL | 071-PG | platform security専用監査流がない |
| API | 072-API | Reviewer向け無制限audit APIが残っている |
| PostgreSQL | 073-PG | 追記専用権限・triggerがない |
| PostgreSQL | 074-PG | 冪等scope・版指紋・期限が不足 |
| PostgreSQL | 075-PG | 002 Expand / 003 Contract migrationがない |
| Mobile | 076-MOB | Auditor role、承認ETag、409再審査導線がない |
| OIDC | 077-OIDC | tenant mapping付きの審査ETag完結試験がない |
| DR | 078-DR | 新schema復元検査と3証跡属性がない |
| Domain | 079-DOM | seed固定状態機械不変条件試験と新属性がない |
| API | 080-API | 故障分離された`IAuditSink`とmetricがない |
| Domain | 081-DOM | 差戻し理由と取下げ理由が分離されない |
| API | 081-API | role別DTOの理由可視性が分離されない |
| Performance | PERF-002-PG | 10万件・選択率別plan・P95証跡がない |

## 4. 実行方法

```bash
./scripts/check-test-ids.sh
./scripts/test-stage6r1-red.sh
```

T1の期待は重複0件、22件すべて`STAGE6R_IMPLEMENTATION_MISSING`で赤、harness error 0件である。結果は`docs/evidence/stage6r1-red-result.json`へ保存する。

## 5. 限界とT2への変換条件

Stage 6R-1作成時点では.NET SDKとPostgreSQL実行環境がなかった。その後.NET SDK 10.0.400とPostgreSQL 18.6を導入し、C# Release buildは警告0・エラー0、既存Domain/API/Mobile/OIDCは53合格・TC-055期待RED 1件まで再確認した。PostgreSQL server、RLS、DRはコンテナの実効UID変更禁止により未実行である。T1 contractは要求の欠落を機械的に固定するもので、acceptance合格を代替しない。

T2では各contractを次のnative behavioral testへ置換する。

1. Domain 3件をC#状態機械試験へ移す。
2. API 8件を実Kestrelとtest authentication/tenant fixtureで実行する。
3. PostgreSQL 7件を非所有application role、接続pool再利用、実migrationで実行する。
4. Mobile/OIDC/DR/Performance各1件を対応suiteへ移す。
5. native testへ移したIDはPython registryから除き、1 ID 1 executable sourceを維持する。

## 6. Gate判定

- T0 文書・ADR承認: 完了。
- T1 テストのみ追加: 本書、JSON証跡、ID検査が揃えば完了。
- T2 実装: 未着手。
- T3 全85件再実行: 未着手。
- T4 外部受入: 未着手。

赤は欠陥を確認した証拠であり、製品の合格数へ含めない。既存基準線63件もこの環境では再実行していないため、今回の成果物で「85件合格」とは表現しない。

## Stage 6R-5移管記録

`TC-ACC-MVS01-072-API`はAuditor専用tenant監査APIのnative挙動試験へ移管したため、本registryから除外した。残存contractは10件であり、`--assert-red`で10/10 expected RED、harness error 0を確認する。

## Stage 6R-6移管記録

`TC-ACC-MVS01-070-API`、`071-API`、`071-PG`、`080-API`はPlatform Security監査境界のnative挙動試験へ移管した。APIの実装前は既存37件GREEN・新規3件RED、実装後は40/40 GREENである。PostgreSQL TC-071-PGはassembly build済みで、非root CIの実測待ちである。registryの残存contractは6件となり、`--assert-red`で6/6 expected RED、harness error 0を確認する。
