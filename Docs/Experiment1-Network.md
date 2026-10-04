# 実験① PC通知（STEP 5）

## 接続

Unityアプリを起動中の端末をTCPサーバーとし、PCから接続します。
既定ポートは5055です。ExperimentSystemのNotificationReceiverで変更できます。
PCとBeam Proは同一Wi-Fi/LANを使用します。

EditorでPlayした後、PCのPowerShellから次を実行します。

```powershell
& '.\Tools\Experiment1\Send-Notification.ps1' -TargetAddress 127.0.0.1
```

Beam Proへ送る場合は、127.0.0.1を端末のWi-FiのIPv4アドレスに置き換えます。
ポートを変更した場合は `-Port 5055` の値も合わせます。
端末側アプリが前面で動作している状態で使用します。バックグラウンド通知ではありません。
Editorを別PCから使う場合は受信PCのファイアウォールで当該ポートを許可する必要があります。
Wi-Fiの端末間通信が隔離されているネットワークでは接続できません。

## プロトコル

1接続で1行または複数行のASCIIコマンドを送信します。行末はLFまたはCRLFです。
`SHOW_NOTIFICATION\n` で通知します。大小文字は区別されます。
正常な形式のコマンドには `QUEUED SHOW_NOTIFICATION\n` を返します。
この応答は通信キューへの受付を示します。表示・参加者の選択・完了を意味しません。
介入中の再通知はCSVのnotification_received、notification_ignored_busyに記録され、画面を上書きしません。

形式は英大文字・数字・アンダースコアで1〜64文字です。
不正形式はERROR INVALID_COMMAND、256バイトを超える行はERROR LINE_TOO_LONG、
受信キュー上限はERROR QUEUE_FULLを返します。改行前の切断はコマンドとして扱いません。
接続の無通信タイムアウトは3秒です。同時接続は順番に処理します。

SHOW_DOG / PLAY_DOG_HAPPY / HIDE_ALL は今後の拡張用で、STEP 5では動作しません。
未実装・未知コマンドも通信形式が正しければQUEUEDを返しますが、Routerが警告を出して介入状態を維持します。
新しい機能はNotificationCommandRouter、またはCommandReceivedを購読する条件別アダプターに追加します。

## CSVとスレッド

通信スレッドはUnityのGameObject・UIに触れません。受信した行と受信時刻だけをキューへ入れます。
メインスレッドのUpdateからRouterを呼び、Controller.ReceiveNotificationへ接続します。
notification_receivedは端末側で改行まで受信した時刻です。PC送信時刻ではありません。
prompt_shownはUIのPanelを有効化した時刻です。描画完了の時刻ではありません。
保存手順はExperiment1-Logging.mdを参照してください。

## Android権限

既存プロジェクトの生成済みAndroidManifestにはINTERNET権限が含まれていることを確認しました。
STEP 5ではProjectSettings・XR設定・Manifestを変更していません。
実験側のAndroidビルドの生成Manifestは、端末確認前に改めて検証します。

## 検証

Tools > Experiment 1 > Verify TCP Notification Flowで、実TCPのlocalhost接続と両経路、CSVを確認します。
結果はLibrary/ExperimentStep5-result.txtです。
LAN越しの接続・Beam Proの表示・タッチ操作は端末での確認が必要です。
通知の背景と見せ方は後で調整し、このステップでは変更しません。
