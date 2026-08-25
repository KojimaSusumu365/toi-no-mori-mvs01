# Stage 6R-9 ローカル失敗先行証跡

- 文書ID: QF-EVD-ST6R9-MVS01-RED-001
- 実行日: 2026-08-25
- 判定: **EXPECTED RED**
- 実行環境: .NET SDK 10.0.400、Release

| Gate | GREEN | RED | 判定 |
|---|---:|---:|---|
| Build | warning 0 / error 0 | 0 | 合格 |
| 試験ID一意性 | 1 | 0 | 合格 |
| Domain | 12 | 0 | 合格 |
| API | 41 | 0 | 合格 |
| Mobile | 7 | 0 | 合格 |
| OIDC | 7 | 1 | TC-077だけ期待RED |
| 残存registry | 0 | 2 | 2/2 expected RED、harness error 0 |
| Stage 6R-9 CI構成契約 | 6 | 0 | 合格 |
| root fail-closed | 1 | 0 | native未開始、exit 2、accepted=false |

## REDの意味

`TC-ACC-MVS01-077-OIDC`は、署名・issuer・audience・PKCE・nonce・MFAが正しい未登録組織のID tokenに対し、現行BFFがCookieを一度発行することを検出した。管理APIは後段で403にするが、Stage 6R-9はtenant mappingをtoken検証時へ移し、session作成そのものを拒否する。

同じ試験内で、次の既存境界はGREENである。

- 登録済み外部組織は内部tenant UUIDへmappingされ、問いを保存できる。
- EditorとReviewerの両roleを持つ同一`sub`は、詳細strong ETagが一致しても自己承認を403で拒否される。
- 同一tenantへmappingされた異なるReviewer `sub`は承認できる。

したがってREDはCookie発行前tenant mappingの不足に限定され、OIDC protocol、ETag、Domain自己承認境界の故障ではない。

## CI Run #1 確認結果

- Run ID: `32798362811`
- head SHA: `e5b288b97f8252d73817d17a925757110a3f78d1`
- 結果: expected failure
- Domain: 12/12 GREEN
- API: 41/41 GREEN
- Mobile: 7/7 GREEN
- OIDC: 7/8、TC-077だけRED
- PostgreSQL: 12/12 GREEN
- DR: 4/4 GREEN
- Artifact ID: `9545671697`
- Artifact SHA-256: `41d4a625d7433442953244d03d77b9a6de2b6528487d3ea758d43dd3987e93ad`

Run #1は全suiteを継続観測した後に非0を返し、Stage 6R-9の実装対象だけがREDであることを固定した。
