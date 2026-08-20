# Stage 6R-4 DB Security Package Manifest

- Package: `19_toi-no-mori-mvs01-stage6r4-db-security-v0.1.zip`
- Date: 2026-08-20
- Baseline: `18_toi-no-mori-mvs01-stage6r4-tenant-boundary-v0.1.zip`
- Failure-first: 承認済み85件を変更せず補助TC-066-APIを追加し、35 PASS / 1 REDを確認
- Configuration GREEN: API 36/36
- Connection delta: application/migration `NpgsqlDataSource`を分離し、異usernameと双方`VerifyFull`を強制
- Privilege delta: applicationを`NOINHERIT`、非owner、非superuser、非`BYPASSRLS`、DDLなし、最小DMLへ制限
- Startup delta: migration後にapplication資格情報でcatalog診断し、逸脱時は起動拒否
- PostgreSQL test delta: TC-066-PGへsuperuser・owner・`BYPASSRLS`拒否を追加
- Local runner delta: admin/application/migration/BYPASSRLS試験ロールを分離生成
- Required gate: `scripts/test-stage6r4-db-security.sh`はAPI 36件とPostgreSQL 10件を連続実行し、DB未実行なら成功扱いにしない
- PostgreSQL result: 10件build済み、実行0件。実効UID制約によりexit 2、未合格
- Remaining Stage 6R contracts: 11 expected RED

本packageはDBロール境界の実装成果物であり、実PostgreSQL GREENを示す成果物ではない。非root CIでnative 10件を実行して受入を閉じる必要がある。
