# Stage 6R-11R review request for Claude

Status: DRAFT  
Target commit: `4537085c25ed3178214b0693afac7e42ce1b64de`  
Target tree: `4402dd93d1a50fe58e96d0fa0242e30cdcc6450e`  
Baseline PR: #1

## 目的

90/90 GREENという実行結果だけで「変更不要」と結論せず、Forest–Town境界、tenant前提、RLS pool reuse、audit、Run identityを実装と証跡から再判定してください。

## Scope

- Public Readのsingle-tenant前提とArchitecture Gate
- 404/429/503/timeout/DNSの公開契約
- Town側のQuestion body/title保持制約
- rejected pathのAudit Row
- PostgreSQL RLSとpool reuse
- 計画Test IDと実行evidenceの対応
- commit/tree/merge ref/Run/Jobの身元

## Non-scope

- Virtual Town runtimeの実装
- ForestとTownのDB結合
- 新しいBYPASSRLS role
- mainへのmerge
- Stage 6R-12の開始

## 再確認する既知項目

| ID | 現在の仮説 | Claudeに求める判定 |
|---|---|---|
| RVR-N10 | archived 85件表記と現行90/90 gateの対応が曖昧 | current scripts・JSON・summary・workflowを照合 |
| RVR-N11 | head commitとPR merge refの混同 | Run 33002851599の実評価対象を型付きで確定 |
| RVR-N12 | `NULLIF(current_setting(...), '')::uuid` とpool reuse testは既存 | 全RLS policyとCI経路を再検証 |
| RVR-N13 | BYPASSRLS横断集計案はsecurity boundaryと両立しない | Public Read構成値gateへの差替えを評価 |

## 回答方法

[REVIEW-PROTOCOL.md](../../governance/REVIEW-PROTOCOL.md) のFinding形式で [claude-findings.md](claude-findings.md) を置き換えてください。確認済み事実、推論、質問を分離し、必ずtarget SHAと根拠pathを示してください。
