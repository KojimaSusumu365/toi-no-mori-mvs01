# External review protocol

## 1. Review request

依頼者はStage、scope、非scope、target commit、既知のopen questions、必須回答形式を固定します。review開始後にtargetが進んだ場合、そのreviewはstaleです。

## 2. Finding形式

```text
ID: RVR-NNN
Severity: P0 | P1 | P2 | Note
Category: Security | Contract | Evidence | CI | Architecture | Documentation
Target SHA:
Claim:
Evidence:
Impact:
Required closure:
Confidence: confirmed | inferred | question
```

## 3. 応答status

- `ACCEPTED`
- `REJECTED_WITH_REASON`
- `DEFERRED_WITH_OWNER_REASON_DUE`
- `POLICY_DECISION_REQUIRED`
- `CLOSED_VERIFIED`

Codexの「修正済み」はCLOSEではありません。Claudeの再検証とUserのacceptanceを経て `CLOSED_VERIFIED` になります。

## 4. Closure gate

- P0/P1に未解決がない
- 計画Test IDと実績Test IDの対応がある
- rejected pathのaudit rowを含む
- not-runが0でない場合は理由・owner・期限がある
- Run identityが[SOURCE-OF-TRUTH.md](SOURCE-OF-TRUTH.md)を満たす
- Userがfinal acceptanceを記録する
