# Stage 6R-4C Non-root PostgreSQL CI Package Manifest

- Package: `20_toi-no-mori-mvs01-stage6r4c-nonroot-postgresql-ci-v0.1.zip`
- Date: 2026-08-20
- Baseline: `19_toi-no-mori-mvs01-stage6r4-db-security-v0.1.zip`
- Workflow: Ubuntu 24.04の非root preflight後、API 36件とPostgreSQL native 10件を必須gateとして実行
- Security: repository read-only、checkout credential非保持、`pull_request_target`不使用、外部actionを40桁SHAへ固定、CI内`sudo`不使用
- Toolchain: .NET 10.0.400 / PostgreSQL 18.6をchecksum固定し、build済みtoolchainはcacheしない
- Evidence: run metadata、runner UID、toolchain、suite件数、log SHA-256をJSON/Markdown/logで保存
- Contract test: CI構成・証跡判定6/6 GREEN
- Regression: Release build警告0/エラー0、API 36/36、試験ID一意性GREEN
- Local fail-closed: root環境でnative suite未開始、exit 2を確認
- GitHub native result: 未実行。PostgreSQL 10/10のartifact取得までDB受入は未完了

本packageはCI構築成果物であり、GitHub Actions上の実DB10/10合格証跡ではない。repositoryへ反映して最初のnative runを完了し、required status checkへ設定する必要がある。
