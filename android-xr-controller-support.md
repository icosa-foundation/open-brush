# Android XR controller support

Status of controller input on Android XR builds (`XrSdkMode.AndroidXR`), and what is
still missing. Written against `com.unity.xr.androidxr-openxr` 1.4.1 and
`com.unity.xr.openxr` 1.18.0.

## What Android XR controllers actually bind to

Unity's Android XR documentation
(`com.unity.xr.androidxr-openxr/Documentation~/features/androidxr-support.md`,
"Controllers") lists exactly two supported controller interaction profiles:

| Profile | Path |
| :--- | :--- |
| Oculus Touch Controller Profile | `/interaction_profiles/oculus/touch_controller` |
| Khronos Simple Controller Profile | `/interaction_profiles/khr/simple_controller` |

Both require `android.hardware.xr.input.controller` in the Android manifest.

This matters because Open Brush picks its input bindings from the *device name* Unity
reports, and that name comes from the interaction profile that bound, not from the
runtime or the hardware.

## The path that already works

`OculusTouchControllerProfile Android` is enabled in
`Assets/XR/Settings/OpenXRPackageSettings.asset`, and
`OpenXRSettings.Features/OculusTouchControllerProfile` registers its device as
`"Oculus Touch Controller OpenXR"`.

That name flows through the existing chain unchanged:

1. `VrSdk.SetUnityXRControllerStyle` matches `device.name.Contains("Oculus Touch")`
   and sets `ControllerStyle.OculusTouch`.
2. `UnityXRControllerInfo.SetActionMask` maps that style to
   `actionSet.OculusTouchControllerScheme.bindingGroup`.
3. The Oculus Touch scheme in `UnityXRInputAction` binds trigger, grip, thumbstick,
   and both face buttons.

So an Android XR controller that binds via the Oculus Touch profile should work with
no code changes. This is the expected case for Android XR devices shipping
controllers, and it should be confirmed on hardware before any of the work below is
started — most of the gap list may already be moot.

## The gaps

### 1. No fallback for an unrecognised device (the real hole)

`VrSdk.SetUnityXRControllerStyle` is a chain of device-name substring tests. The final
`else` only logs:

```csharp
Debug.LogWarning("Unrecognised controller device name: " + device.name);
```

No style is set, so the controller style stays at the `ControllerStyle.InitializingUnityXR`
placeholder that `Start()` assigned, and the app sits with no controller geometry and
no binding mask. A device that binds only `khr/simple_controller` reports
`"KHR Simple Controller OpenXR"`, matches nothing, and lands here.

There is already a `TODO:Mikesky` in `VrSdk.CreateControllerInfo` noting the same
thing ("set to return the default instead"). Defaulting the `else` to a sane style —
`OculusTouch` is the most broadly bound scheme — would turn a dead controller into a
mostly-working one for any future runtime, not just Android XR.

### 2. Khronos Simple cannot drive Open Brush even with a binding scheme

`KHRSimpleControllerProfile` exposes only `select` (click), `menu` (click), grip/aim
poses and `haptic`. There is no trigger axis, no grip axis, no thumbstick and no
touchpad. `UnityXRControllerInfo` reads `TriggerAxis`, `GripAxis`, `ThumbAxis`,
`PadAxis`, `PadTouch`, `PrimaryButton` and `SecondaryButton`.

Adding a generic KHR-simple binding scheme would therefore produce a controller that
can point and click but cannot paint, size a brush, or scroll a panel. Worth doing as
a "does not hang on an unknown device" safety net; not worth doing as a supported
input path.

### 3. Manifest feature declaration

The two profiles above are only offered by the runtime when the built manifest
declares `android.hardware.xr.input.controller`. Unity's Android XR feature injects
manifest entries from the enabled feature set, so this likely comes for free with
`AndroidXRSupportFeature`, but the generated `AndroidManifest.xml` should be checked
for that `uses-feature` line — and for whether it is marked `required="false"`, since
a controllers-required manifest would restrict Play Store availability on
hands-only devices.

### 4. No Android XR specific profile exists to add

There is no Samsung/Android XR controller interaction profile in the OpenXR or
Android XR packages — a search for `/interaction_profiles/` across
`com.unity.xr.androidxr-openxr` returns only the two paths above. So there is nothing
device-specific to enable in `OpenXRPackageSettings.asset` or in
`BuildTiltBrush.TempSetOpenXrFeatureGroup`. Controller support is entirely a question
of the two generic profiles.

## Suggested order of work

1. Build for Android XR, run on device with controllers paired, and read the log. If
   `SetUnityXRControllerStyle` reports `Oculus Touch Controller OpenXR`, controllers
   already work and only item 1 is worth doing (as hardening).
2. If the log shows `Unrecognised controller device name: ...`, note the name and
   either add it to the matcher or implement the default-style fallback in item 1.
3. Treat items 2 and 3 as follow-ups driven by what the device actually reports.

## Related

Hand tracking on Android XR is handled separately, on
`feature/unity6-handtracking-clean`, by `Assets/Hands/AndroidXRHandBridge.cs`, which
substitutes tracked hands for the controller abstraction entirely. That branch exists
because of the gaps above: `VrSdk.IsInitializingUnityXR` short-circuits to `false`
when the bridge is active, precisely to escape the stuck `InitializingUnityXR` state
described in item 1.
