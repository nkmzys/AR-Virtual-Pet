# Dog Animation Preview Coordinates

Date: 2026-06-10

Scene: `Assets/Work/DogAnimationTest/Dog_AnimationPreview.unity`

Working result:

- Dog faces the Game view camera correctly after reloading the scene.
- `my_pome_1.controller` starts from `077_Idle_Loop`.
- This scene is for animation preview only. It does not use AR Session, XR Origin, ARTrackedImageManager, or DogMarkerPlacement.

Main Camera:

```text
Position: X 0, Y 0.65, Z 2.8
Rotation Euler Hint: X 10, Y 180, Z 0
Rotation Quaternion: X 0, Y 0.9961947, Z -0.087155744, W 0
```

Pomeranian_AnimationPreview:

```text
Prefab: Assets/Dog_Pomeranian/Base/Pomeranian.prefab
Animator Controller: Assets/Dog_Pomeranian/Animator/my_pome_1.controller
Controller GUID: 0ed01e5af45ea054897e4d08a2d00903
Scene root rotation override:
  Rotation Quaternion: X 0, Y 0, Z 0, W 1
  Rotation Euler Hint: X 0, Y 0, Z 0
```

Note:

If the dog appears backward again, stop Play mode and reload `Dog_AnimationPreview.unity`. Unity does not always apply external scene-file edits while Play mode is running.

For the AR scene, do not copy this preview camera setup. Use `DogMarkerPlacement` settings, especially `Local Rotation Offset Euler Y`, if the AR dog faces the wrong way.
