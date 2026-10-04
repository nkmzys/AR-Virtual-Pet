# 実験① CSVログ（STEP 4）

## 保存場所

Unityの `Application.persistentDataPath/Experiment1/Logs` に起動ごとのCSVを作成します。
PlayするとConsoleに `[ExperimentLogger] CSV: ...` と実際の絶対パスが表示されます。
Windows Editorでは通常ユーザーのAppData/LocalLow配下、Androidではアプリ固有の外部ファイル領域です。
端末の実際のパスとADBでの回収はSTEP 7で確認します。追加の共有ストレージ権限は要求しません。
ファイル名に日時とランダムIDを含め、既存ログを上書きしません。

## CSV形式

`timestamp,event` の2列、UTF-8です。時刻はタイムゾーン付きISO 8601形式です。
例：`2026-10-02T10:00:00.1234567+09:00,accepted`
端末の時計とタイムゾーンを使用します。PCと端末の時計同期は実機実験前に確認してください。
表示イベントの時刻はPanelを有効化した時刻で、ディスプレイの走査・描画完了時刻ではありません。
各行を即時書き出し、Play停止・Component無効化時にファイルを閉じます。
保存失敗はConsoleへ明示されます。失敗後もUIは動作しますがCSV保存は停止します。

## イベント

| event | 意味 |
|---|---|
| waiting_shown | 起動直後の待機表示 |
| notification_simulated | Inspectorのローカル通知操作 |
| notification_received | 将来のPC受信。STEP 4では通信がないため発生しない |
| notification_ignored_busy | 介入中の通知を無視した |
| prompt_shown | 通知Panelを表示した |
| accepted | 「実施する」を押した |
| exercise_shown | 実施手順Panelを表示した |
| declined | 「今回は行わない」を押した |
| exercise_completed | 「完了」を押した |
| finish_shown | 終了Panelを表示した |
| waiting_returned | 介入終了後に待機へ戻った |

## Editorでの確認

1. Experiment1Textを開きPlayする。
2. ConsoleでCSVの保存先を確認する。
3. ExperimentSystemのInterventionControllerのメニューからShow Notification (Local Preview)を実行する。
4. 「実施する」→「完了」、待機後に再通知→「今回は行わない」を試す。
5. Playを停止し、CSVを開いて両経路のイベントと時刻を確認する。

自動確認は `Tools > Experiment 1 > Verify State Flow in Play Mode` から実行できます。
検証では実際のButton.onClickを呼び出します。マウス・タッチ操作そのものの確認は手動です。
検証結果は `Library/ExperimentStep4-result.txt` へ保存します。

## 後で対応する項目

背景が暗いため、通知の見せ方・色・文言の提示方法を後から調整します。
STEP 4では見た目を変更しません。PC通信はSTEP 5で追加します。
