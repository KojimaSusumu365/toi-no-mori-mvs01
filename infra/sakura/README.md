# さくらのクラウド Stage 3/4設定境界

ここには秘密を含まない設定例だけを置く。現時点では実クラウドへresourceを作成するIaCではない。

## 人が先に確認する項目

1. 石狩第1/東京第1のsite accountを別々に準備する。
2. 石狩source bucketと東京destination bucketを作り、双方のversioningを有効化する。
3. 石狩sourceから東京destinationへCRRを設定する。
4. CRR設定後にtest objectをuploadし、東京到達を確認する。設定前のobjectは自動複製されない。
5. upload主体とrecovery主体を別credentialにし、bucket/prefixごとに最小権限を設定する。
6. `dr-config.example.env` の非秘密値をdeployment設定へ移す。
7. access key/secret keyはAWS CLI標準credential providerまたは秘密管理基盤へ置く。repositoryやshell historyへ書かない。
8. 復旧秘密鍵は東京側だけに置き、石狩backup workerには公開証明書だけを配る。

## Stage 4 アプリケーション認証設定

1. OIDC providerへ機密Web clientを登録し、Authorization Code + PKCE、MFAを必須にする。
2. redirect URIを外部公開originの`/signin-oidc`、post-logout URIを`/signout-callback-oidc`へ完全一致で登録する。
3. `sub`、`amr=mfa`、`role=Editor|Reviewer|Auditor|PlatformAuditor`を発行・mappingする。tenant AuditorとPlatformAuditorを同じgroupへ割り当てない。
4. API 2台から同じData Protection key ringを読める非公開共有領域を用意する。
5. key ring保護用PFXとpasswordを秘密管理基盤から注入し、API実行主体以外には読ませない。
6. `application-config.example.env`の非秘密値だけをdeployment設定へ移す。
7. DB接続文字列はapplication、migration、platform audit writer、platform audit readerの4種類を別secretとして注入する。usernameを共用しない。
8. `Audit__PartitionHashKey`は32byte以上のrotation可能な秘密として注入し、DB、repository、ログへ値を残さない。
9. Load BalancerでTLS終端する場合、既知proxyだけを信頼するforwarded headers設定を実装・検証してから公開する。

## Stage 6R-4 PostgreSQLロール境界

- migrationロールは管理schemaのDDL ownerとし、applicationロールへ必要最小権限をGRANTできること。
- applicationロールは`NOINHERIT`、非superuser、非`BYPASSRLS`、非table-owner、schema `CREATE`なしとする。
- applicationロールへは`questions`の`SELECT/INSERT/UPDATE`、`question_revisions`と`audit_events`の`SELECT/INSERT`、`idempotency_records`の`SELECT/INSERT/DELETE`、`tenants`の`SELECT`、必要sequenceだけを与える。
- `schema_migrations`、DDL、`TRUNCATE`、`REFERENCES`、`TRIGGER`はapplicationロールへ与えない。
- Production起動時にcatalog診断が一項目でも不一致なら起動を拒否する。診断を無効化する設定は設けない。
- `compose.postgres.yml`と`infra/postgres/init-roles.sh`はローカル検証用であり、本番passwordを`.env`やrepositoryへ保存しない。

## Stage 6R-6 Platform Security監査境界

- `platform_security_events`はtenant業務表と分離し、tenant ID、subject、生IP、claim、本文、token、Cookieを保存しない。
- platform audit writerはINSERTだけ、readerはSELECTだけ、application roleは全権限なしとする。
- IdPの`PlatformAuditor`だけが期間必須APIを使用し、tenant `Auditor`には403を返す。
- 429は不可逆partition hash・正規化action・UTC 1分窓で抑制し、監査書込み障害で元の429を変更しない。

Stage 4は設定契約とローカルBFF/UIまでを実装している。実IdP、Load Balancer、共有key ring、secret注入はまだ構築していない。設計判断は`../../docs/architecture/adr/adr-0004-mobile-bff-oidc.md`を正とする。

## Stage 5 IdP候補

初期Editor/Reviewer向けmanaged IdPはMicrosoft Entra IDを第一候補とする。app本体はgeneric OIDCのまま、`roles`とMFA証跡claimを設定で切り替える。実tenant接続前に`../../docs/architecture/identity/entra-id-setup.md`のENTRA-AT-01〜10を実施する。

Stage 5では署名付き試験IdPとのHTTPS OIDC往復を実装済みだが、実Entra tenantへresourceを作成していない。実ID tokenでMFA証跡を確認するまで、`amr=mfa`を確定値として扱わない。証跡が一致しなければ管理機能は403で停止させ、検査を外さない。

## 未実装のIaC gate

Provider/APIのresource schemaと変更計画を実アカウントでread-only検証してからIaCを追加する。`apply`は人のreview、plan保存、対象project/zone/bucketの二重確認を通す。Stage 3は資格情報がないため、推測したTerraform resourceを成果物へ含めない。

運用手順は `../../docs/architecture/dr/dr-runbook.md`、設計判断は `../../docs/architecture/adr/adr-0003-ishikari-primary-tokyo-recovery.md` を正とする。
