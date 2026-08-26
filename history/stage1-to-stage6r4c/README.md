# 問いの森 CORE — Stage 1〜Stage 6R-4C 履歴アーカイブ

このディレクトリは、MVS-01 の Stage 1 から Stage 6R-4C までに作成したコード、試験、設計資料、ADR/UML、運用・DR資料を、後から再検証・再構築できるようにGitHubへ保存する履歴アーカイブです。

## 保存方式

GitHub ConnectorからDOCX/ZIPのバイナリ原本を直接ストリーム転送できないため、Stage別ZIPを展開して得られた**全UTF-8ファイルの差分を、復元可能なunified patch系列として保存**します。Stage別ZIPの中身はすべてUTF-8テキストであり、ソース、試験、Markdown仕様、ADR/UML、SQL、Shell、JSON証跡を含みます。

- `patches/`: Stage 1 → 6R-4Cを順番に再構築するパッチ系列。
- `governance/`: Stage 6R技術レビュー受入・ADR/テスト計画の独立overlay。
- `foundation/`: DOCX原本から抽出したGitHub検索可能なMarkdown版。
- `ORIGINAL_SHA256SUMS.txt`: ChatGPT Libraryに保存されているDOCX/ZIP原本のSHA-256。

## Stage対応

| 順序 | Stage | パッチ |
|---:|---|---|
| 1 | Stage 1 | `patches/07_toi-no-mori-mvs01-aspnet-core-v0.1.patch` |
| 2 | Stage 2 | `patches/08_toi-no-mori-mvs01-postgresql-v0.2.patch` |
| 3 | Stage 3 | `patches/09_toi-no-mori-mvs01-dr-v0.3.patch` |
| 4 | Stage 4 | `patches/10_toi-no-mori-mvs01-mobile-bff-v0.4.patch` |
| 5 | Stage 5 | `patches/11_toi-no-mori-mvs01-oidc-e2e-v0.5.patch` |
| 6 | Stage 6 | `patches/12_toi-no-mori-mvs01-mobile-workflow-v0.6.patch` |
| overlay | Stage 6R設計統制 | `governance/13_toi-no-mori-stage6r-review-acceptance-v0.1.patch` |
| 7 | Stage 6R-1 | `patches/14_toi-no-mori-mvs01-stage6r1-red-tests-v0.1.patch` |
| 8 | Stage 6R-1 Toolchain | `patches/15_toi-no-mori-mvs01-stage6r1-local-toolchain-v0.1.patch` |
| 9 | Stage 6R-2 | `patches/16_toi-no-mori-mvs01-stage6r2-domain-v0.1.patch` |
| 10 | Stage 6R-3 | `patches/17_toi-no-mori-mvs01-stage6r3-approval-api-v0.1.patch` |
| 11 | Stage 6R-4 | `patches/18_toi-no-mori-mvs01-stage6r4-tenant-boundary-v0.1.patch` |
| 12 | Stage 6R-4 DB Security | `patches/19_toi-no-mori-mvs01-stage6r4-db-security-v0.1.patch` |
| 13 | Stage 6R-4C | `patches/20_toi-no-mori-mvs01-stage6r4c-nonroot-postgresql-ci-v0.1.patch` |

## 復元例

空ディレクトリでGitリポジトリを初期化し、Stage順に適用します。

```bash
mkdir restored && cd restored
git init

git apply ../patches/07_toi-no-mori-mvs01-aspnet-core-v0.1.patch
git add -A && git commit -m 'restore Stage 1'

git apply ../patches/08_toi-no-mori-mvs01-postgresql-v0.2.patch
git add -A && git commit -m 'restore Stage 2'
# 09 → 10 → 11 → 12も同様

# Stage 6R設計統制は独立overlay。必要なら適用する。
git apply ../governance/13_toi-no-mori-stage6r-review-acceptance-v0.1.patch

git apply ../patches/14_toi-no-mori-mvs01-stage6r1-red-tests-v0.1.patch
# 15 → 16 → 17 → 18 → 19 → 20を順番に適用
```

Stage 13のgovernance patchは`governance/`配下へ資料を追加するだけで、Stage 6の製品treeを削除しません。Stage 6R-1のパッチはStage 6の製品treeを基準に生成しています。

## 基礎資料

`foundation/`には次のDOCX原本をMarkdownへ展開した内容を保存します。

- `toi-no-mori-requirements-v0.1`
- `toi-no-mori-system-basic-spec-v0.1`
- `01_toi-no-mori-development-charter-v0.1`
- `02_toi-no-mori-public-disclosure-policy-v0.1`
- `03_toi-no-mori-minimum-system-detailed-spec-v0.1`
- `04_toi-no-mori-mvs-01-iteration-spec-v0.1`
- `05_toi-no-mori-mvs-01-acceptance-test-spec-v0.1`
- `06_toi-no-mori-mvs-01-uml-spec-v0.1`

原本DOCXのSHA-256を各Markdownと`ORIGINAL_SHA256SUMS.txt`へ記録します。UML仕様DOCX等の埋め込み画像はバイナリ転送制約のため直接格納せず、Stage別パッチ内のMarkdown UMLを正本として追跡します。

## 現行コードとの関係

このフォルダは**履歴保全用**です。現行実装はリポジトリ直下の`src/`, `tests/`, `docs/`, `scripts/`, `infra/`を正とし、履歴パッチを直接編集して現行コードへ反映しません。
