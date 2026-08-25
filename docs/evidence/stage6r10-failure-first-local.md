# Stage 6R-10 ローカル失敗先行証跡

- 文書ID: QF-EVD-ST6R10-MVS01-RED-001
- 実行日: 2026-08-25
- 判定: **LOCAL PRECHECK ACCEPTED / NATIVE REDはCI待ち**
- 実行環境: .NET SDK 10.0.400、PostgreSQL 18.6配置済み、root制約環境

| Gate | 結果 |
|---|---:|
| Build | warning 0 / error 0 |
| 試験ID一意性 | GREEN |
| Stage 6R-10 CI構成契約 | 6/6 GREEN |
| 残存failure-first registry | 1/1 expected RED、harness error 0 |
| root fail-closed wrapper | exit 2、accepted=false |
| native DR 4/5 | 非root GitHub Actionsで実行待ち |

## REDの狙い

TC-030〜033を維持したまま、TC-078だけが次の不足でREDとなるように組み込んだ。

- restore reportにmigration 005、`fk_published_revision_same_question`、`platform_security_events`の機械判定がない。
- 異subject二者承認、source停止、復元受入、route切替の時系列validatorがない。
- canonical artifactと`artifactHash` sealがない。

ローカル環境はrootから`nobody`への切替を許可しないため、native PostgreSQLを迂回せずexit 2とした。環境停止をTC-078のREDとは数えず、非root GitHub Actionsで既存DR 4件GREEN・TC-078だけREDを確認して追記する。

## GitHub Actions Run #1

- Run ID: `32801014338`
- Job ID: `97661671512`
- head SHA: `14a0a05c5b388da34a39c8c15e25221efec5d56c`
- Domain: 12/12 GREEN
- API: 41/41 GREEN
- Mobile: 7/7 GREEN
- OIDC: 8/8 GREEN
- PostgreSQL: 12/12 GREEN
- DR: 4/5。TC-078だけ期待RED
- native合計: 84/85
- Artifact ID: `9546527037`
- Artifact SHA-256: `748a819ade10ec23dc989df89c5d09521468019e5bab0c63499ee21f7e7bdd8c`

構成、ツールチェーン、既存84件は合格しているため、REDをStage 6R-10の実装不足へ限定できた。
