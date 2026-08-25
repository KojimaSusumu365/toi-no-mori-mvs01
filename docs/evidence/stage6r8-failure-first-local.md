# Stage 6R-8 ローカル失敗先行証跡

- 文書ID: QF-EVD-ST6R8-MVS01-RED-001
- 実行日: 2026-08-25
- 判定: **EXPECTED RED**
- 実行環境: .NET SDK 10.0.400、Release

| Gate | GREEN | RED | 判定 |
|---|---:|---:|---|
| Build | warning 0 / error 0 | 0 | 合格 |
| 試験ID一意性 | 1 | 0 | 合格 |
| API | 40 | 1 | TC-081だけ期待RED |
| Mobile | 6 | 1 | TC-076だけ期待RED |
| 残存registry | 0 | 3 | 3/3 expected RED、harness error 0 |
| Stage 6R-8 CI構成契約 | 6 | 0 | 合格 |
| root fail-closed | 1 | 0 | native未開始、exit 2、accepted=false |

## REDの意味

- `TC-ACC-MVS01-081-API`: 現行の汎用管理DTOがEditor応答へ`ownerSubject`を返し、role別許可リストになっていない。
- `TC-ACC-MVS01-076-MOB`: 現行画面は一覧中のversionから`If-Match`を組み立て、審査詳細応答のETagを保持していない。

既存API 40件とMobile 6件はGREENであり、toolchain、test runner、既存機能の失敗ではない。GREEN実装前の基準点として保存する。

## CI Run #1の観測改善

Run #1はAPI TC-081の期待REDを検出した時点で既存runnerが停止し、Mobile以降を未実行とした。失敗を成功扱いにはしていないが、複数REDの証跡として不完全なため、`test.sh`と`test-all.sh`を「全suiteを継続実行し、最後に非0を返す」方式へ修正する。Run #2でAPI/Mobile両REDと、その他suiteのGREENを同時に確認する。
