# Experiment 1: Android text condition

## Saved checkpoint (2026-10-04)

- Branch: `experiment1-text`, derived from `experiment1-base`.
- Unity Editor: 6000.4.3f1, Android ARM64 / IL2CPP.
- Scene: `Assets/Experiment/Scenes/Experiment1Text.unity`.
- App display name: `20261004_Experiment1_Text`.
- Android application ID: `jp.arpet.experiment1.text`.
- APK: `Builds/Android/20261004_Experiment1_Text/20261004_Experiment1_Text.apk`.
- Android build succeeded with zero errors. The generated APK includes `android.permission.INTERNET` without forcing a new PlayerSettings permission setting.
- The participant confirmed installation, launch and notification operation on Beam Pro. CSV output on the device has not been separately retrieved or inspected at this checkpoint.

## Condition-specific settings

This branch disables XR automatic initialization and removes the Android XREAL loader and XREAL package dependency. Shared `experiment1-base` retains the original XR/XREAL configuration. The text scene uses an ordinary camera and Canvas, without AR or dog objects.

## Build

Install Unity 6000.4.3f1 with Android Build Support, including SDK, NDK and OpenJDK. From this project's root, run the following in PowerShell (or adjust the Unity executable path):

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.4.3f1\Editor\Unity.exe' -batchmode -nographics -quit -buildTarget Android -projectPath "$PWD" -executeMethod ExperimentAndroidBuild.Build -logFile "$PWD\android-build.log"
```

`Assets/Editor/ExperimentAndroidBuild.cs` explicitly builds only Experiment1Text, independently of the scene list in Build Settings. Check `build-result.txt` alongside the APK for success. APKs and build output are ignored by Git; generate them locally and distribute through Google Drive.

## Device check

1. Upload the APK to Google Drive, download it on Beam Pro and install it.
2. Connect the PC and Beam Pro to the same Wi-Fi and keep the app open in its waiting screen.
3. Check the current Beam Pro IP address; it can change between networks.
4. From the project root, send a notification using the current address:

```powershell
powershell -ExecutionPolicy Bypass -File .\Tools\Experiment1\Send-Notification.ps1 -TargetAddress <BEAM_PRO_IP>
```

5. Check Accept -> Exercise -> Complete -> Finish -> Waiting and Decline -> Finish -> Waiting.

See [network instructions](Experiment1-Network.md), [CSV logging](Experiment1-Logging.md), and [Editor verification](Experiment1-EditorCheck.md).

UI brightness and wording changes are deferred. AR integrations are deferred to separate condition branches.
