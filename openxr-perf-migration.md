# OCULUS_SUPPORTED removal, and reviving the perf controls on OpenXR

Part 1 (removal) is **done**. Part 2 (the OpenXR replacements) is **deferred** and is
written up below in enough detail to be picked up cold.

## Background

`OCULUS_SUPPORTED` was a manual scripting define. It was not in
`ProjectSettings.asset` for any platform, was not set by `BuildTiltBrush.cs`, and was
not set by any CI workflow. `README.md` told developers to add it by hand.

It could not be turned on. The guarded code needed two Meta packages:

| Symbols used | Package | Present |
| :--- | :--- | :--- |
| `Oculus.Platform.*` | `com.meta.xr.sdk.platform` | yes, until removed |
| `OVRManager`, `OVROverlay`, `OVRSpatialAnchor`, `OVRBoundary`, `OVRSpace` | `com.meta.xr.sdk.core` | **no, and never was** |

`com.meta.xr.sdk.core` was not in `Packages/manifest.json`, and `OVRManager` and
friends were defined nowhere in `Assets`, `Packages`, or the package cache. Setting the
define broke the build immediately. Every guarded block was unreachable, so all of it
was removed rather than fixed.

## Part 1 - what was removed (done)

Commits on `integration/unity6-kitchen-sink`:

| Commit | Contents |
| :--- | :--- |
| `3497c7d8e` | `com.meta.xr.sdk.platform` + Meta XR scoped registry out of `manifest.json` / `packages-lock.json`; the four now-pointless `packages_to_remove: com.meta...` lines out of `build.yml` |
| `13d78a1c0` | the `#if OCULUS_SUPPORTED` code in 6 shared files, plus the README section |
| `ce80cff78` | `Assets/OculusMR/` and the unused Logitech line-drawing sample |

Verified after: no `OCULUS_SUPPORTED` anywhere in `Assets` or `README.md`; no `OVR*` or
`Oculus.Platform` symbols left in any `.cs`; all six GUIDs of the deleted scripts and
prefabs have zero remaining references.

### Feature that was deleted

**Colocated multiplayer via Meta spatial anchors.** `Assets/OculusMR` plus the anchor
sharing in `MultiplayerManager` and `PhotonRPC` let several people in one room share a
sketch space. Part 2 does not bring this back. To revive it you would re-add
`com.meta.xr.sdk.core` and recover the files from git history before `ce80cff78`.

### Decisions taken

1. `UpdateDynamicQualityDebugText` and its `OVROverlay` setup: **deleted**, along with
   the `m_DebugText` serialized field. The overlay was only reachable by hand-enabling
   a disabled GameObject in the editor.
2. `LineDrawing.cs` + `Drawing.prefab`: **deleted**. Despite living inside
   `#if OCULUS_SUPPORTED`, the file contained zero `OVR`/`Oculus` symbols - it was
   self-contained `LineRenderer` sample code referenced by nothing outside its own
   folder. The rest of `Assets/ThirdParty/Logitech/` (`VrStylusHandler`,
   `StylusHandler`, the MX Ink prefabs and OpenXR interaction profile) is live and was
   left alone.
3. `ExtraData.OculusPlayerId` (`MultiplayerDataStructs.cs:69`): **kept**. It is part of
   the networked payload, written in `PhotonPlayerRig.cs` and read back there. Removing
   it would change the wire format and break compatibility with clients on older
   builds. It is now hardcoded to 0 at the one place it was populated.

### Loose ends left behind

- **`Assets/Scripts/GUI/GpuTextRender.cs` is now orphaned.** Nothing references the
  class. It was only ever driven by the debug overlay. Safe to delete, but it was left
  in place because the matching scene object needs the editor (below).
- **`Main.unity` still contains a disabled "Debug Performance Overlay" GameObject**
  carrying a `GpuTextRender`, and the `QualityControls` component in that scene still
  serializes an `m_DebugText` reference to a field that no longer exists. Unity drops
  the orphaned property silently on load and on next save. Deleting the GameObject
  wants the editor rather than hand-edited scene YAML.
- **`Config.OculusSecrets` / `Config.OculusMobileSecrets`** (`Config.cs:144-145`) now
  have no consumers. They are part of the `SecretsConfig` surface, so they were left
  alone rather than pulling on the secrets system.
- **The play-area boundary is now always empty.** `VrSdk.RefreshRoomBoundsCache` used
  `OVRManager.boundary`; its `#else` arm was only commented-out SteamVR notes. So
  `HasRoomBounds()` (`App.cs:714`) is always false and `GetRoomBoundsAabb()`
  (`DynamicBounds.cs:42`) always gets an empty AABB. This was already the case before
  the removal, since the define was never set - the code just made it look otherwise.
  OpenXR has no vendor-neutral play-area query, so there is nothing to swap in.

## Part 2 - OpenXR replacements (deferred)

Three `VrSdk` methods were left as honest no-ops with `TODO` comments pointing here:
`SetFixedFoveation`, `GetGpuUtilization`, `SetGpuClockLevel`. All three replacement
features already exist in the project as **disabled** entries in
`Assets/XR/Settings/OpenXRPackageSettings.asset`. Nothing needs installing.

### Why this is worth doing

`QualityLevels Mobile.asset` requests foveation only at the two lowest quality levels:

| Quality level | `m_GpuLevel` | `m_FixedFoveationLevel` |
| :--- | :--- | :--- |
| 0 | 5 | 2 |
| 1 | 5 | 1 |
| 2 | 5 | 0 |
| 3 | 4 | 0 |
| 4 | 3 | 0 |

Those are the rescue levels the dynamic scaler drops to when a headset is struggling -
exactly where foveation would help most, and exactly where it currently does nothing.

### 2.1 Fixed foveated rendering - full replacement

`OVRManager.tiledMultiResLevel` becomes `XRDisplaySubsystem.foveatedRenderingLevel`, a
float 0..1. Unity's docs state Quest maps the float onto its discrete levels, so `0.5`
gives Quest medium; the existing 0-3 API maps onto quarters.

Prerequisites, all already met: Unity 6+, `com.unity.xr.openxr` 1.11+, URP.

1. Enable `FoveatedRenderingFeature` for Android (currently `m_enabled: 0`) and set its
   **Foveated Rendering Method** sub-option to *Foveated rendering (SRP API)*.
2. Implement `VrSdk.SetFixedFoveation(int level)` as
   `foveatedRenderingLevel = level / 3f` on the active display subsystem.

Caveat: on Vulkan, FDM foveation is disabled at runtime if the device lacks
`VK_EXT_fragment_density_map`. The plug-in falls back through other VSR techniques, so
this degrades rather than breaks.

### 2.2 GPU clock level - full replacement

`OVRManager.gpuLevel` becomes
`XrPerformanceSettingsFeature.SetPerformanceLevelHint(PerformanceDomain.Gpu, hint)`
from `XR_EXT_performance_settings`.

Hints available: `PowerSavings` (0), `SustainedLow` (25), `SustainedHigh` (50),
`Boost` (75). `AppQualitySettings.GpuLevel` is `Range(1,5)`, so a mapping has to be
chosen - and note the old field range never matched Quest's 0-4 `gpuLevel` either, so
check what it was really doing before preserving the behaviour.

1. Enable `XrPerformanceSettingsFeature` for Android.
2. Implement `VrSdk.SetGpuClockLevel(int level)` as a call to `SetPerformanceLevelHint`.

It returns `false` when the extension is unavailable, and the feature fails
`OnInstanceCreate` on runtimes that do not support it, so it no-ops safely.

### 2.3 GPU utilisation - no equivalent, change the consumer instead

There is no vendor-neutral OpenXR extension exposing a GPU utilisation number.
`AndroidXRPerformanceMetrics` does, but it is Android XR only, which defeats the point.

The nearest standard mechanism is in the same performance-settings extension, and it is
push-based rather than polled:

```csharp
XrPerformanceSettingsFeature.OnXrPerformanceChangeNotification += OnPerfChange;

// PerformanceChangeNotification
//   PerformanceDomain domain;                        // Cpu | Gpu
//   PerformanceSubDomain subDomain;                  // Compositing | Rendering | Thermal
//   PerformanceNotificationLevel fromLevel, toLevel; // Normal | Warning | Impaired
```

Why it matters: `GetGpuUtilization()` returns `0`, so in
`QualityControls.UpdateDynamicQuality` the counter `m_NumFramesGpuTooHigh` can never
increment and `m_NumFramesGpuLowEnough` increments every frame. The scaler cannot drop
quality for GPU load and is permanently biased toward raising it. Only the FPS arm
works. **This is live today** - it is not something the removal introduced.

Proposed change:

1. Subscribe to the notification and cache the latest GPU `toLevel`.
2. Replace the two GPU frame counters with that cached level: `Warning` or `Impaired`
   allows a quality drop, `Normal` allows a raise.
3. Keep the FPS arm exactly as it is.
4. Either drop `GetGpuUtilization()` or have it report the cached level.

Better than the Quest original in two ways: it separates **thermal** from **rendering**
pressure, and it covers CPU as well as GPU.

## Verification for Part 2

- Android OpenXR and AndroidXR CI builds succeed.
- On a Quest, confirm foveation is active and that `SetPerformanceLevelHint` returns
  `true`. Log both.
- Confirm a performance notification is actually delivered under load before trusting
  the rewritten scaler. If the runtime never sends one, the GPU arm is inert again and
  should be removed rather than left looking functional.
