# Android Desktop-Style Launch Intent Arguments

## Goal

Allow a regular Android build to use the same Open Brush launch arguments as desktop builds, without adding a separate Android intent extra for every argument. The immediate use case is starting an OpenXR build in XR view-only mode without using the dedicated `OPEN_BRUSH_VIEWER` build flavour or editing `/sdcard/Open Brush/Open Brush.cfg`.

The intended launch command is:

```shell
adb shell am force-stop <package>
adb shell am start -W \
  -n <package>/com.unity3d.player.UnityPlayerActivity \
  --es OpenBrushArgs "--ForceViewOnly"
```

View-only and XR are independent here. This launch option must not enable monoscopic mode or disable XR.

## Observed behaviour

On 7 August 2026, `com.Icosa.OpenBrush-PR1069.apk` was installed on a Quest 3S. Its manifest values were:

1. Package: `foundation.icosa.openbrushPR1069`
2. Activity: `com.unity3d.player.UnityPlayerActivity`
3. Version: `2.30.14-g54ab424fb`

Launching with Unity's conventional string extra did not activate view-only mode:

```shell
adb shell am start -W \
  -n foundation.icosa.openbrushPR1069/com.unity3d.player.UnityPlayerActivity \
  --es unity "--ForceViewOnly"
```

The application launched in XR, but painting remained available.

## Root cause

`Assets/Scripts/Config.cs` already recognizes `--ForceViewOnly` inside `ParseArgs()`:

```csharp
else if (args[i] == "--ForceViewOnly")
{
    ParseUserSetting("--Flags.ForceViewOnly", "true");
}
```

However, `Config.Awake()` has a dedicated `UNITY_ANDROID` branch. Before this change, that branch read only these boolean intent extras:

1. `EnableMonoscopicMode`
2. `DisableXrMode`

The Android branch does not call `ParseArgs(System.Environment.GetCommandLineArgs())`. That call is in the later non-Android branch, so the `unity=--ForceViewOnly` string extra never reaches Open Brush's argument parser on Android.

The dedicated Viewer build still enters view-only mode because `UserConfig.Flags.ForceViewOnly` returns `true` when compiled with `OPEN_BRUSH_VIEWER`. A normal OpenXR build does not have that define.

## Implemented change

Android accepts an Open Brush-owned string intent extra named `OpenBrushArgs`:

```shell
adb shell am start -W \
  -n <package>/com.unity3d.player.UnityPlayerActivity \
  --es OpenBrushArgs "--ForceViewOnly --noQuickLoad"
```

`Config.Awake()` reads that string, splits it using the same quoted-argument handling as `m_FakeCommandLineArgsInEditor`, and passes the result to the existing `ParseArgs()` method. Quoted values containing spaces remain one argument:

```shell
--es OpenBrushArgs "--Some.Setting \"value with spaces\""
```

The existing `EnableMonoscopicMode` and `DisableXrMode` boolean extras and activity aliases remain available. They retain their Android-specific early XR handling. Arguments supplied through `OpenBrushArgs` use the shared desktop parser and are intentionally not restricted by an Android allowlist.

`ParseUserSetting()` stores the override until `App.RefreshUserConfig()` calls `Config.ApplyUserConfigOverrides()`. `SketchControlsScript` subsequently reads `App.UserConfig.Flags.ForceViewOnly` during startup and calls `ViewOnly(true)`.

## Manual test procedure

Before testing, remove `ForceViewOnly` from `/sdcard/Open Brush/Open Brush.cfg` or set it to `false`. The attached Quest currently has it set to `true` as a workaround; leaving it enabled would produce a false-positive test.

Use a regular Android OpenXR APK, not the Android Viewer artifact.

1. Install the APK and identify its actual package name with `aapt dump badging`.
2. Force-stop the package before every launch. `Config.Awake()` runs only when the process starts.
3. Launch normally, with no `ForceViewOnly` extra. Confirm the application starts in XR and painting is available.
4. Force-stop it again.
5. Launch with `--es OpenBrushArgs "--ForceViewOnly"`.
6. Confirm head tracking remains active.
7. Confirm the fly/navigation tool is selected, editing panels and the painting pointer are unavailable, and painting is not possible.
8. Force-stop and launch normally once more. Confirm editing mode returns when neither the intent extra nor config enables view-only.
9. Re-test `EnableMonoscopicMode` and `DisableXrMode` intent launches to confirm their existing behaviour is unchanged.
10. Test a quoted value and at least one additional desktop argument through `OpenBrushArgs`.

On Quest, the first launch of a newly installed package may be intercepted by the system's Controller Required dialog. Continue through it before judging application state. Verify that the pending launch preserves the string extra.

## Diagnostic logging

The implementation logs `[OB_ANDROID_ARGS] Parsing OpenBrushArgs intent extra` when the extra is present. Search the complete process log for `[OB_ANDROID_ARGS]` when confirming that the launch intent reached Open Brush.

## Scope

The functional change is in `Assets/Scripts/Config.cs`. It does not require:

1. A new build flavour.
2. `OPEN_BRUSH_VIEWER` in the regular OpenXR build.
3. Manifest or activity-alias changes.
4. Disabling XR or enabling monoscopic mode.
5. Changes to the existing view-only UI and tool restrictions.
6. A separate Android intent extra for every desktop launch argument.

The existing config-file workaround is:

```json
{
  "Flags": {
    "ForceViewOnly": true
  }
}
```

That workaround proves the regular OpenXR APK can run in XR view-only mode; only the Android launch-time input is missing.
