# Mobile AR handoff ? 2026-09-29

Actual build project: `AR-Virtual-Pet`, branch `mobile-ar`.
Unity Editor.log records output `AR-Virtual-Pet/20260929_mobile_ar.apk`.
Earlier assistant edits were made in the separate `20260714_dog_pome_health_behavior_mobile_ar` project and were NOT included in this build. Do not assume those edits are present here.

Historical notes reviewed: `CODEx_HANDOFF_MARKER_DOG.md`, `CODEX_HANDOFF_DOG_ANIMATION_20260610.md` in dated projects; `Assets/Work/DogAnimationTest/Dog_AnimationPreview_COORDS.md`. The working preview uses a camera on +Z facing -Z and identity dog rotation; the mobile camera is on -Z. Do not copy preview camera coordinates or edit Root/Bip001 bones.

Current change: SimpleMarkerDogDetector previously forced Y=180 at marker placement. Changed to identity (180-degree reversal), with LateUpdate preserving outer placement rotation after Animator evaluation. No bone, clip, or prefab changes. This corrects the actual build source; device-facing result still needs verification. The prior unsuccessful APK does not establish an animation rotation bug because it used unchanged source.

Rebuild from this project and verify initial display and subsequent behavior switching. User prefers a quick test APK without folder reorganization. Existing unrelated user settings changes must be preserved.

## Facing v2 ? supersedes the fixed identity correction above

User still saw the back after rebuilding. The Android IL2CPP output contains the previous LateUpdate method, so source inclusion is confirmed for the latest local build (installed APK cannot be verified: adb has no connected devices).
The historical preview camera is on +Z; mobile camera is on -Z. Fixed identity is not a reliable front-facing correction.
Now SimpleMarkerDogDetector creates an unanimated parent pivot, samples Bone_Nose minus Bone_tail_00 after the first animated frame in LateUpdate, and rotates the parent about world Y to align that horizontal direction with the camera. Calibration runs once so deliberate turns in the clips are preserved. No bone rotations or animation files are changed. Runtime log: [MobileDogFacing v2].
Compilation passed against local Unity assemblies. Actual device orientation remains unverified. Rebuild the AR-Virtual-Pet project; suggested distinct test name: mobile-ar-facing-v2.apk.

## Inspector tuning after successful facing test

User confirmed Facing v2 faces forward on device. Added Forward Tilt Degrees (positive pitches the entire dog forward/down toward the viewer) and live Marker Dog Scale updates. Tilt is an absolute offset on the unanimated pivot after yaw calibration; default zero preserves the verified orientation.
Editor-only Preview Without Marker displays the animated dog at centered-marker placement in Play mode, using the same calibration path. This flag and preview bypass are compiled out of APK builds. Use Game view with portrait aspect ratio. Adjust the detector component, not animated bones. Play-mode values revert on Stop: record scale/tilt and re-enter them after stopping, then save the scene. This tilts the entire body; it is not eye/head gaze tracking.
