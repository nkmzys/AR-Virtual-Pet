# 実験① Editor操作確認（STEP 6）

## 確認した内容

- PC側Send-Notification.ps1で127.0.0.1:5055へ送信し、Promptを表示。
- UnityのGame画面に実際のマウス入力を送り、「実施する」→「完了」をクリック。
- Finishの「PCの作業に戻ってください。」を約3秒表示してWaitingへ復帰。
- 再通知後、「今回は行わない」を実際にクリックし、同様に待機へ復帰。
- Waiting / Prompt / Exercise / Finishのキャプチャで日本語文言・ボタン表示を確認。
- CSVは13イベントの想定順とISO形式の時刻を検証。
- Play停止後にCSVファイルを排他読取できること、TCPポートを再利用できることを確認。

キャプチャ・状態情報・結果はLibrary/ExperimentStep6-*に保存しています。
Editor確認用ExperimentEditorCheck.csはEditor専用で、実機アプリには含まれません。
STEP 6では実行用Script・Scene・UI・XR設定・ProjectSettingsの内容を変更していません。

## 手動で再確認する方法

1. 新しいworktreeをUnity 6000.4.3f1で開く。
2. Assets/Experiment/Scenes/Experiment1Text.unityを開いてPlayする。
3. ConsoleのNotificationReceiverのListeningメッセージと、ExperimentLoggerのCSV保存先を確認。
4. worktreeのフォルダでPowerShellを開き、次を実行する。

```powershell
powershell -ExecutionPolicy Bypass -File .\Tools\Experiment1\Send-Notification.ps1 -TargetAddress 127.0.0.1
```

5. Gameタブで両方の選択経路を試す。終了画面のあと約3秒で待機へ戻る。
6. Playを停止し、Consoleに表示されたCSVを確認する。

## 既存SDKのEditor上の問題

Windows EditorのPlay起動時、既存XREAL SDKのXREALCallbackHandler.OnLoadから
DllNotFoundException: XREALXRPluginが発生しています。
Experimentの通知・マウス操作・CSVフローはこの例外後も動作しましたが、Console無エラーの状態ではありません。
以前のShader Graphのコンパイルエラーとは別の実行時例外です。
共通親であるexperiment1-baseのPackage・XR設定を保持する方針に沿い、今回はSDKを変更していません。
条件別の設定変更とSDK対応は分けて扱います。Android/Beam Proでの挙動はSTEP 7で確認します。

## 未確認・後日対応

Beam Pro実機へのビルド、生成ManifestのINTERNET権限、LAN接続、タッチ操作、実機日本語表示、ログ回収はSTEP 7です。
背景が暗い点と通知の見せ方は後で調整します。
