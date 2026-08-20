# Stage 6R-2 Domain層 赤→緑 仕様・実施記録

> 履歴文書: 本文の「TC-064は赤」「残存19件」はStage 6R-2終了時点の記録である。現在状態は`stage6r3-approval-api-red-green.md`を参照する。

- 文書ID: QF-ST6R2-MVS01-001
- 版: Version 0.1
- 日付: 2026-08-20
- 入力基準: 承認済みADR-0007〜0010、Stage 6R-1失敗先行22契約
- 判定: **Domain 3契約をnative挙動試験へ移管し、赤→緑を完了**

## 1. 反復の境界

本反復はQuestion集約だけを製品変更範囲とする。APIの承認`If-Match`、外部組織IDから内部tenantへの許可表変換、PostgreSQLのtenant列・RLS・複合外部キー、role別DTOは次の反復に残す。

Domainへ`TenantId`を導入したことだけではテナント分離を達成しない。固定`TenantIds.Mvs01`はStage 6R-1保存形式と既存APIを壊さないための移行境界であり、本番のセキュリティ境界として扱わない。

## 2. 仕様と試験の対

| 仕様 | Domain実装 | 対になる試験 | 合格条件 |
|---|---|---|---|
| ADR-0008-D1/D2 承認対象版固定 | `Approve(reviewer, expectedVersion, now)`、`ApprovedVersion`、`ApprovedBy` | TC-ACC-MVS01-063-DOM | 空Reviewer、自己承認、古い版を無変更で拒否。現在版だけを公開し承認版・承認者を保持 |
| DOMAIN-INVARIANTS 集約不変条件 | `TenantId`、版進行、Snapshot/Rehydrate | TC-ACC-MVS01-079-DOM | seed `20260820`、500系列×20操作。成功は版を1だけ進め、拒否は全属性不変、tenantは常に不変 |
| ADR-0008-D4 理由分離 | `ReviewReason`、`WithdrawalReason` | TC-ACC-MVS01-081-DOM | 差戻しと取下げの理由を混用しない。空の取下げ理由を安定コードで拒否 |

## 3. Domain契約

### 3.1 識別子と版

- `Id`と`TenantId`は空GUIDを許可しない。
- 集約の`TenantId`は生成後に変更できない。
- 成功したcommandは`Version`を正確に1だけ増やす。
- Domain規則違反で拒否したcommandは、状態・版・日時・理由・承認情報を一切変更しない。

### 3.2 承認

承認は`IN_REVIEW`で、Reviewerが空でなく、所有者本人でなく、`expectedVersion == Version`の時だけ成功する。成功直前の版を`ApprovedVersion`へ、Reviewer subjectを`ApprovedBy`へ保存して`PUBLISHED`へ遷移し、その後集約版を1進める。

既存API向けの`Approve(reviewer, now)`は現在版を渡す互換オーバーロードとして残す。これはAPIの事前条件制御を代替しない。API側で`If-Match`を必須化するTC-ACC-MVS01-064-APIは引き続き赤である。

### 3.3 理由

- 差戻し: `ReviewReason`へ保存し、`WithdrawalReason`は設定しない。
- 再編集・再申請・承認: `ReviewReason`を消去する。
- 取下げ: `WithdrawalReason`へ保存し、`ReviewReason`は使用しない。
- 空の取下げ理由: `question.withdrawal.reason_required`で拒否する。

### 3.4 旧保存形式との互換

Stage 6R-1の11属性Snapshotを読み戻せるよう、新属性は末尾の省略可能引数とした。旧データでtenantが欠ける場合だけ固定`TenantIds.Mvs01`へ正規化する。既存公開データの承認者・承認版backfillはDB Expand/Contract migrationで別途設計・検証する。

## 4. 赤→緑の実施

1. Pythonの静的契約からDomain 3 IDを外し、C# native suiteへ移した。
2. 製品実装前にDomain test projectをビルドし、承認overload・新属性・tenant付きconstructor欠落による23件のC#エラーを確認した。
3. 最小Domain実装を追加した。
4. Domain 12件を再実行し、12合格・0失敗を確認した。
5. API、Mobile、OIDC、全solution build、試験ID検査、残存19契約を回帰確認した。

詳細結果は`docs/evidence/stage6r2-domain-result.md`と`docs/evidence/stage6r2-remaining-red-result.json`に保存する。

## 5. 再実行

```bash
./scripts/test-stage6r2-domain.sh
```

このgateはDomain 12件の緑、試験ID重複0、残存19契約の期待REDを同時に確認する。残存REDを製品合格へ数えない。

全.NET回帰は次で実行する。

```bash
./scripts/build.sh
dotnet run --project tests/ToiNoMori.Api.Tests -c Release --no-build
dotnet run --project tests/ToiNoMori.Mobile.Tests -c Release --no-build
dotnet run --project tests/ToiNoMori.OidcE2e.Tests -c Release --no-build
```

## 6. Gate判定

- T0 文書・ADR承認: 完了。
- T1 22件の失敗先行契約: 完了。
- T2-Domain 3件のnative化と実装: 完了。
- T2-API/PG/Mobile/OIDC/DR/Performance 19件: 未完了、期待RED。
- T3 全85件: 未実施。
- T4 外部受入: 未実施。

次の小さな反復は、承認APIの`If-Match`必須化（064-API）とtenant resolver（065-API）を赤→緑にするStage 6R-3を推奨する。DB RLSを伴わないtenant対応は本番分離と見なさず、PostgreSQL反復と一体で受入gateを設ける。
