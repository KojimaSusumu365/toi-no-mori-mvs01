# MVS-01 災害復旧Runbook

- Version: 0.3
- 日付: 2026-08-16
- 対象: 石狩本番、東京コールド復旧
- 暫定目標: RPO 1時間以内、RTO 4時間以内

## 0. 安全原則

- 一人で災害切替を決定しない。災害責任者と技術責任者の二者承認を記録する。
- 旧石狩DBへの書込み停止またはネットワーク隔離を確認するまで、東京をwrite primaryにしない。
- 復号・署名検証・内部SHA-256検証のどれか一つでも失敗したバックアップを使わない。
- 復旧先は空のDB/別データ領域とし、証拠保全が必要な既存領域を上書きしない。
- access key、DB password、秘密鍵をrunbook、シェル履歴、チケット、ログへ貼らない。
- 自動試験のRPO/RTOと実クラウド訓練のRPO/RTOを混同しない。

## 1. 役割

| 役割 | 責務 |
|---|---|
| Incident Commander | 災害宣言、優先順位、二者承認の一方、対外連絡 |
| Recovery Lead | 旧本番隔離、復元、整合性確認、二者承認の一方 |
| Security Reviewer | 鍵利用承認、監査証跡、侵害疑いの判定 |
| Service Owner | 問い・監査の業務整合性、RPO内損失の承認 |
| Communications | 状況、影響、次回更新時刻を公開。秘密や推測を出さない |

## 2. 平常時の前提確認

日次で監視し、四半期ごとに復旧訓練する。

1. 石狩第1サイトと東京第1サイトに別バケットを作成する。
2. 両バケットでversioningを有効にする。
3. 石狩ソースへCRRを設定してから最初のバックアップを置く。CRR有効化前の既存objectは自動複製されない。
4. バックアップ実行主体は石狩bucketの所定prefixへput/headだけを許可する。
5. 復旧主体は東京bucketの所定prefixをreadできるが、石狩本番DBへ接続できないよう分離する。
6. 復旧秘密鍵が石狩に存在しないこと、東京と緊急保管の双方から復旧手順で利用できることを確認する。
7. 最新backupの生成成功、石狩head、CRR completed、東京head、サイズ/SHA-256一致を監視する。
8. 1時間以内に検証済み東京objectがない場合はRPO警報を上げる。

公式の現行endpoint/region:

| サイト | endpoint | region |
|---|---|---|
| 石狩第1 | `https://s3.isk01.sakurastorage.jp` | `jp-north-1` |
| 東京第1 | `https://s3.tky01.sakurastorage.jp` | `jp-east-1` |

## 3. 定期バックアップ

資格情報と鍵は秘密管理基盤から短時間だけ注入する。DB passwordは権限600のpassfileを使う。

```bash
export POSTGRES_BIN_DIR=/opt/postgresql/bin
export MVS01_SOURCE_PGHOST=db.internal.example
export MVS01_SOURCE_PGPORT=5432
export MVS01_SOURCE_PGUSER=backup_role
export MVS01_SOURCE_PGDATABASE=toi_no_mori
export MVS01_SOURCE_PGPASSFILE=/run/secrets/pgpass
export MVS01_DR_OUTPUT_DIR=/var/lib/toi-no-mori/backups
export MVS01_DR_SIGNER_CERT=/run/secrets/backup-signer.crt
export MVS01_DR_SIGNER_KEY=/run/secrets/backup-signer.key
export MVS01_DR_RECIPIENT_CERT=/etc/toi-no-mori/tokyo-recovery.crt

backup_file="$(./scripts/dr/create-encrypted-backup.sh)"
metadata_file="${backup_file%.p7m}.metadata.json"
```

生成後は平文dumpを保持しない。scriptは一時ディレクトリを終了時に削除し、暗号文を権限600で確定する。

## 4. 石狩uploadと東京CRR確認

AWS CLIの標準credential providerを使用する。アクセスキーをコマンド引数にしない。

```bash
export MVS01_DR_BACKUP_FILE="$backup_file"
export MVS01_DR_METADATA_FILE="$metadata_file"
export MVS01_DR_SOURCE_BUCKET=replace-with-ishikari-bucket
export MVS01_DR_DESTINATION_BUCKET=replace-with-tokyo-bucket
export MVS01_DR_OBJECT_PREFIX=production/hourly

object_key="$(./scripts/dr/upload-and-await-crr.sh)"
```

commandが0以外で終了した場合、バックアップ生成成功としても東京復旧可能とは記録しない。object key、両サイトのversion ID、ReplicationStatus、確認時刻、ciphertext SHA-256を証跡へ残す。秘密値は残さない。

## 5. 災害宣言

時刻はすべてUTCで記録する。

1. 監視障害、さくら公式障害情報、利用者影響を相互確認する。
2. Incident Commanderがincident ID、宣言時刻、想定影響、最終正常バックアップを記録する。
3. Recovery Leadが石狩APIをmaintenance/read-onlyへ移行し、DB書込み経路を遮断する。
4. 石狩の生存が不明な場合も、東京側のGSLB切替はまだ行わない。
5. 最新の東京側CRR完了objectを選び、snapshot開始時刻との差をRPO見積りとして記録する。
6. 目標RPO超過または侵害疑いがある場合、より古い既知正常versionも候補にし、Service Owner/Security Reviewerの判断を記録する。

## 6. 東京での隔離復元

東京bucketから選択した`.p7m`を、権限700の隔離領域へ取得する。外部metadataのSHA-256とサイズを確認するが、最終的にはscript内のGCM/CAdES/internal manifest検証を信頼境界とする。

空の復旧DBを作成し、次を実行する。

```bash
export POSTGRES_BIN_DIR=/opt/postgresql/bin
export MVS01_DR_BACKUP_FILE=/recovery/incoming/selected-backup.p7m
export MVS01_DR_RESTORE_DIR=/recovery/work/selected-backup
export MVS01_DR_RECIPIENT_CERT=/run/secrets/tokyo-recovery.crt
export MVS01_DR_RECIPIENT_KEY=/run/secrets/tokyo-recovery.key
export MVS01_DR_TRUSTED_SIGNER_CERT=/etc/toi-no-mori/ishikari-backup-signer.crt
export MVS01_TARGET_PGHOST=tokyo-db.internal.example
export MVS01_TARGET_PGPORT=5432
export MVS01_TARGET_PGUSER=restore_role
export MVS01_TARGET_PGDATABASE=toi_no_mori
export MVS01_TARGET_PGPASSFILE=/run/secrets/pgpass

restore_report="$(./scripts/dr/restore-encrypted-backup.sh)"
jq . "$restore_report"
```

失敗時は同じrestore directoryを再利用しない。証跡を保全し、新しい空ディレクトリと空DBを作る。検証を迂回するopenssl/pg_restoreの直接実行は禁止する。

## 7. 復元後の受入確認

すべて合格するまで東京APIを公開しない。

1. DB migration countが期待値以上で、最新schema IDがアプリversionと一致する。
2. question、audit、idempotencyの件数を直近証跡と比較する。
3. sentinel問いまたは承認済み代表データを公開APIで取得できる。
4. 下書き/レビュー中データが公開APIから404となる。
5. 管理APIはOIDC、CSRF、権限、自己承認禁止、監査を維持する。
6. `/health/live` と `/health/ready` が200となる。
7. 503応答やログに接続文字列・鍵・トークンが出ない。
8. snapshot開始時刻から災害宣言までをRPO、災害宣言から上記完了までをRTOとして記録する。

## 8. トラフィック切替

1. 旧石狩write経路が隔離されている証跡を二者で確認する。
2. Incident CommanderとRecovery Leadが東京primary化を承認する。
3. 東京APIをwrite-enabledにする。
4. GSLBの応答先を東京へ変更する。
5. 外部ネットワークからスマートフォン相当幅とPCの公開GET、認証、更新、監査をsmoke testする。
6. エラー率、p95 latency、DB接続、ディスク、WAL、監査欠落を継続監視する。
7. 利用者へ復旧時刻、既知のデータ損失範囲、次回更新時刻を通知する。

## 9. 切戻し

石狩復旧直後にGSLBだけ戻してはならない。東京で発生した新規書込みを正とし、石狩へ新しい基準バックアップまたは承認したレプリケーション方法で同期する。石狩の隔離復元、整合性確認、二者承認、write role移行、GSLB切替の順とする。双方を同時にwrite-enabledにしない。

## 10. 訓練と証跡

ローカル手順検査:

```bash
POSTGRES_BIN_DIR=/path/to/postgresql/bin \
  MVS01_DR_EVIDENCE_DIR="$PWD/docs/evidence" \
  ./scripts/test-disaster-recovery.sh
```

最低限保存する証跡:

- test IDと合否
- backup ID、snapshot開始/完了、暗号文SHA-256
- CRR完了、東京object version、到達確認時刻
- 災害宣言、旧本番隔離、復元完了、API受入、GSLB切替のUTC時刻
- RPO/RTO実測、目標との差
- 承認者の主体ID。秘密、本文、credentialは含めない
- 問題、是正担当、期限、次回再試験

ローカル試験の`docs/evidence/dr-drill-latest.json`は再現可能性の証拠であり、実クラウド訓練証跡の代替ではない。
