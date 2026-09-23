# AR-Virtual-Pet

研究用のUnity ARバーチャルペットプロジェクトです。Android向けにXREAL Beam ProとXREAL Air 2 Ultraで動作確認しています。

## Required Environment

- Unity 6000.4.3f1 (Unity 6.4)
- Android Build Support
- XREAL Unity SDK 3.1.0
- XREAL Beam Pro
- XREAL Air 2 Ultra

## XREAL SDK Setup

XREAL SDK本体は、このGit repositoryには含まれていません。利用者自身がXREAL公式Developerサイトから「XREAL Unity SDK 3.1.0」を取得してください。

ダウンロードした `com.xreal.xr.tar.gz` を、次の場所へ配置します。

```text
ThirdParty/XREAL/com.xreal.xr.tar.gz
```

このtarballはGit管理対象外です。`Packages/manifest.json` は、プロジェクトの `Packages` フォルダを基準にした相対パスでSDKを参照します。

## Expected SDK

- Package: `com.xreal.xr`
- Version: `3.1.0`
- File: `com.xreal.xr.tar.gz`
- SHA256: `FD7D0FCE103712E7ACC85BFCBB8B127007E1D84F64596EFB6C11580BEA058D90`
- Size: `248169074` bytes

## Opening the Project

1. GitHubからこのrepositoryをCloneします。
2. XREAL公式DeveloperサイトからXREAL Unity SDK 3.1.0を取得します。
3. `com.xreal.xr.tar.gz` を `ThirdParty/XREAL/` に配置します。
4. Unity HubからUnity 6000.4.3f1でプロジェクトを開きます。
5. Unity Package ManagerによるPackage解決が完了するまで待ちます。
6. Build PlatformがAndroidであることを確認します。
7. 必要に応じてBeam ProへBuild & Runします。

## Git Management

Gitで管理する主なファイルは `Assets/`、`Packages/`、`ProjectSettings/`、`.gitignore`、`README.md`です。

`Library/`、`Logs/`、`Temp/`、`Obj/`、`Build/`、`Builds/`、`UserSettings/`、IDE生成ファイル、`*.csproj`、`*.sln`、`*.slnx`、`*.apk`、`*.aab`などの生成物はGit管理しません。XREAL SDKのtarballも再配布せず、Git管理しません。
