# Stage 6R-5 Draft PR Acceptance Manifest

- Baseline: Draft PR #1 / head `d8522d51cdd9f40fb3330e7cb7b6623cdca54214`
- Failure-first: Mobile TC-055 RED、native API TC-072-API REDを実測
- Security change: ReviewerとAuditorを分離し、tenant限定・上限付き・許可リスト監査APIへ移行
- Local GREEN: Domain 12/12、API 37/37、Mobile 6/6、OIDC E2E 7/7、警告0・エラー0
- CI contract: Stage 6R-4C 6/6、Stage 6R-5 8/8
- Native gate: non-root、exact-count 76/76、PostgreSQL 10/10、DR 4/4、immutable artifact
- Fail closed: rootではnative suite未開始、evidence rejected、exit 2
- Remaining failure-first contracts: 10件。Draft PR受入と製品全体完成を混同しない
- Remote result: GitHub Actions実行後に追記する
