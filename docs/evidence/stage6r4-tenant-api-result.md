# Stage 6R-4 Tenant API 赤→緑証跡

- 実行日: 2026-08-20 UTC
- 構成: Release / .NET SDK 10.0.400
- 対象: TC-ACC-MVS01-065-API、TC-ACC-MVS01-069-API

## RED

製品実装前にAPI suite 35件を実行し、33件合格・2件失敗を確認した。

- TC-065: claim欠落要求の期待403に対し201 Created。
- TC-069: 他所有者更新の期待404に対し403 Forbidden。

この段階ではテストID重複を避ける移管前なので、Stage 6R Python registryは変更していない。

## GREEN

`TenantResolver`、`RequireTenantFilter`、tenant付きStore契約、InMemory分離、404正規化を実装後、API 35/35が合格した。追加ループで404本文差を検出し、他所有者・他tenant・不存在を同じProblem Detailsへ統一してtype/title一致とID非開示を再確認した。

回帰結果:

- Solution Release build: 警告0、エラー0
- Domain: 12/12 GREEN
- API: 35/35 GREEN
- OIDC E2E: 7/7 GREEN
- Mobile: 5/6、既知TC-055だけ期待RED
- 試験ID一意性: PASS
- Stage 6R残存: 11/11 expected RED、harness error 0

PostgreSQL native試験5件はこの証跡へ合格として含めない。
