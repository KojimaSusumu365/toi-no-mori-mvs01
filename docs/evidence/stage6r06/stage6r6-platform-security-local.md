# Stage 6R-6 ローカルRED→GREEN証跡

- 実行日: 2026-08-20
- Build: 0 warnings / 0 errors
- RED: 既存API 37/37、新規TC-070/071/080が失敗、API合計37/40
- GREEN: API 40/40
- Domain: 12/12
- Mobile: 6/6
- OIDC E2E: 7/7
- CI構成契約: 6/6
- 試験ID一意性: PASS
- 残存failure-first registry: 6/6 expected RED、harness error 0

この証跡はローカル非DB 65件の合格である。PostgreSQL TC-071-PGを含む11件とDR 4件は、このroot制約環境では実行していない。GitHub Actionsの非root native 80/80 artifactが得られるまでStage 6R-6全体合格とはしない。
