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
