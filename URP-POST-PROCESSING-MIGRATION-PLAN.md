# URP Post Processing Migration Plan

## Scope

This plan covers replacing the current Built-in Render Pipeline post-processing hooks with URP-compatible post-processing while preserving the existing Open Brush quality model:

- Desktop quality levels select bloom off/fast/full, HDR, FXAA, MSAA, render scale, and stroke simplification.
- Mobile quality levels dynamically change quality from frame rate/GPU utilization and fade bloom in or out over time.
- Capture/dropcam/Lua camera post-processing toggles still need to work.
- Existing video/screenshot/render-wrapper assumptions around HDR, mask generation, recording, and quality changes need to be handled explicitly.

## Current Legacy Behavior

### Quality data

Quality settings are serialized in:

- `Assets/QualityLevels.asset`
- `Assets/QualityLevels Mobile.asset`
- `Assets/Scripts/AppQualitySettingLevels.cs`

Each quality level contains:

- `BloomMode`: `None`, `Fast`, `Full`, `Mobile`
- `Hdr`
- `Fxaa`
- `MsaaLevel`
- `BloomLevels` for mobile bloom downsample chain
- viewport and eye texture scale
- LOD, anisotropic filtering, simplification, GPU clock, and foveation settings

Current desktop values:

- Level 0: bloom off, HDR off, FXAA off
- Level 1: bloom fast, HDR on, FXAA on
- Level 2: bloom full, HDR on, FXAA on
- Level 3: bloom full, HDR on, FXAA on, MSAA 4

Current mobile values all have `m_Bloom: 0`, so mobile bloom is currently disabled by quality settings even though the mobile bloom system still exists.

### QualityControls runtime flow

`Assets/Scripts/QualityControls.cs` is the central switchboard.

On `Init()` it collects cameras and legacy components:

- `SENaturalBloomAndDirtyLens`
- `FXAA`
- `MobileBloom`

On `SetQualityLevel()` it:

- calls `SetBloomMode(settings.Bloom)`
- calls `EnableHDR(settings.Hdr)`
- calls `EnableFxaa(settings.Fxaa)`
- updates shader LOD, MSAA, anisotropic filtering, simplification, XR viewport scale, XR eye texture scale, GPU clock, fixed foveation
- calls `QualitySettings.SetQualityLevel(...)`
- raises `OnQualityLevelChange`

On mobile, `Update()` dynamically changes `QualityLevel` based on FPS and GPU utilization thresholds. It also fades `m_MobileBloomAmount` toward `m_DesiredBloom` over `AppQualityLevels.BloomFadeTime`, applying the value to each `MobileBloom.BloomAmount`.

### Desktop legacy bloom

`Assets/ThirdParty/Sonic Ether/Natural Bloom/Scripts/SENaturalBloomAndDirtyLens.cs` implements bloom entirely through `OnRenderImage`.

Behavior:

- Creates an `ARGBHalf` temporary chain.
- Clamps source into an HDR-ish buffer.
- Downsamples to 4 octaves in low quality, 8 octaves in full quality.
- Applies blur passes.
- Blends bloom and optional lens dirt back into the destination.
- The serialized XRRig values currently use low-quality bloom with low intensity:
  - `bloomIntensity: 0.05`
  - `bloomScatterFactor: 0.5`
  - `lensDirtIntensity: 0.05`
  - `inputIsHDR: 1`
  - `lowQuality: 1`

QualityControls currently enables desktop bloom when mode is `Fast` or `Full`, but it does not set `lowQuality`. That means the practical distinction between `Fast` and `Full` depends on the component's serialized state or any other code path not found during this investigation.

Under URP, this `OnRenderImage` path should not be treated as active.

### Desktop legacy FXAA

`Assets/ThirdParty/jintiao_FXAA/FXAA.cs` is also an `OnRenderImage` blit using `FX/FXAA`.

QualityControls enables or disables the component based on the quality setting's `Fxaa` flag.

Under URP, this should be replaced by URP camera antialiasing or pipeline MSAA policy.

### Mobile legacy bloom

`Assets/Scripts/Rendering/MobileBloom.cs` uses Built-in pipeline command buffers:

- `Camera.AddCommandBuffer(CameraEvent.AfterEverything, ...)` for stereo cameras
- `Camera.AddCommandBuffer(CameraEvent.BeforeImageEffects, ...)` for mono screenshot/dropcam cameras
- `BuiltinRenderTextureType.CameraTarget`
- `CopyTexture` to and from the camera target

The mobile algorithm is performance-oriented:

- Bloom is restricted to a central portion of the eye texture.
- One stereo eye gets newly generated bloom while the other eye reuses saved bloom from the previous frame.
- `BloomLevels` controls the downsample chain, e.g. `2,3,3`.
- `BloomAmount` is driven by `QualityControls` and fades in/out over time.
- Bloom is reduced against bright gradient sky backgrounds using `m_BackgroundBrightnessToBloom`.

This is not a URP renderer feature and should not be expected to work reliably in URP.

### Capture post effects

`CameraConfig.PostEffects` backs the UI/Lua/API toggle:

- Lua: `App:PostProcessing(true/false)`
- Global command: `ToggleCameraPostEffects`
- PlayerPref key: `Camera_PostEffects`

`Assets/Scripts/Rendering/PostEffectsToggle.cs` currently toggles:

- `TiltShift`
- `Kino.Vignette`

Both are `OnRenderImage` effects, so this capture toggle does not map to URP yet.

### Screenshot and dropcam special cases

`ScreenshotManager` and `DropCamPreviewScreen` explicitly manipulate legacy bloom components on mobile:

- force mobile camera HDR off
- disable `MobileBloom` for screenshot manager
- enable `MobileBloom` for dropcam preview
- disable `SENaturalBloomAndDirtyLens`

Those paths need a URP replacement that can target individual capture cameras without depending on `OnRenderImage`.

### RenderWrapper coupling

`Assets/Scripts/Rendering/RenderWrapper.cs` is still coupled to legacy post effects:

- tracks `FXAA` and `SENaturalBloomAndDirtyLens` as render features
- disables them during some recording paths
- uses `HasHdrDecodePass()` to decide whether to enable `HDR_EMULATED`
- uses `OnRenderImage` for desktop blits and selection mask generation

This is a migration risk. Even if URP bloom is added, `RenderWrapper` still contains legacy assumptions about where post-processing and HDR decode happen.

## Migration Direction

Use URP's built-in post-processing for the first migration pass:

- URP `VolumeProfile` with `Bloom`, `Tonemapping`, `ColorAdjustments`, `Vignette`, and optional `ChromaticAberration`.
- `UniversalAdditionalCameraData.renderPostProcessing` per camera.
- URP camera antialiasing for FXAA/SMAA where available.
- URP pipeline MSAA and existing `QualityControls.MSAALevel` for hardware MSAA.

Do not port `SENaturalBloomAndDirtyLens`, `FXAA`, `TiltShift`, `Kino.Vignette`, or `MobileBloom` directly in the first pass unless URP built-in effects fail a visual/performance check. A custom `ScriptableRendererFeature` should be a second pass, mainly for mobile's old eye-alternating bloom behavior if URP bloom is too expensive.

## Proposed Runtime Architecture

### New component: `UrpPostProcessingController`

Create a controller responsible for applying Open Brush quality/post-effect state to URP.

Suggested location:

- `Assets/Scripts/Rendering/UrpPostProcessingController.cs`

Responsibilities:

- Register cameras similarly to `QualityControls.Init()`, but operate on `UniversalAdditionalCameraData` and URP Volumes.
- Own or reference a runtime-instanced `VolumeProfile`.
- Apply quality settings:
  - bloom enabled/intensity/quality from `BloomMode`
  - HDR flag to camera and URP camera data where applicable
  - antialiasing from `Fxaa`
  - post-processing enabled/disabled from `CameraConfig.PostEffects`
  - mobile bloom fade amount
- Subscribe to:
  - `QualityControls.OnQualityLevelChange`
  - `CameraConfig.PostEffectsChanged`
- Provide explicit methods for screenshot/dropcam/video paths to temporarily disable or override post-processing on a camera.

Keep the controller separate from `QualityControls` initially to reduce migration risk. Once stable, `QualityControls` can stop tracking legacy components and delegate post processing to this controller.

### Volume profile model

Use one baseline URP profile asset plus runtime copies.

Suggested assets:

- `Assets/Settings/PostProcessing/OpenBrushPostProfile.asset`
- optionally `OpenBrushCapturePostProfile.asset`

At runtime, instantiate the profile before modifying it. Avoid editing the asset instance directly during play mode.

Profile components:

- `Bloom`
- `Tonemapping`
- `ColorAdjustments`
- `Vignette`
- optional `ChromaticAberration`

Initial values should be conservative:

- Bloom off: intensity `0`
- Bloom fast: low intensity, fewer iterations/skip iterations, lower quality filtering
- Bloom full: slightly higher intensity and better filtering
- Tonemapping: start neutral unless visual comparison shows old HDR encode/decode requires compensation
- Vignette/chromatic aberration: controlled by `CameraConfig.PostEffects`, default off until visual target is chosen

### Quality setting mapping

Keep `AppQualitySettingLevels.BloomMode` as the public data model for now.

Recommended mapping:

| Legacy setting | URP behavior |
| --- | --- |
| `BloomMode.None` | `Bloom.active = false` or intensity `0`; controller disables mobile bloom fade target |
| `BloomMode.Fast` | `Bloom.active = true`, low intensity, `highQualityFiltering = false`, reduced iterations/skip iterations |
| `BloomMode.Full` | `Bloom.active = true`, higher quality values |
| `BloomMode.Mobile` | same controller path as fast/full, but use mobile-tuned values and fade amount |
| `Hdr = false` | camera `allowHDR = false`; URP camera HDR off where available; bloom off unless explicitly allowed |
| `Hdr = true` | camera HDR on; URP asset already supports HDR |
| `Fxaa = true` | set URP camera antialiasing to FXAA/SMAA if supported; otherwise document unsupported fallback |
| `Fxaa = false` | set URP camera antialiasing to none |
| `MsaaLevel` | continue using existing MSAA handling, but verify URP pipeline asset quality settings also reflect it |

For mobile dynamic quality:

- Continue to let `QualityControls` change `QualityLevel`.
- Preserve `m_DesiredBloom` and fade semantics, but move the resulting bloom amount into `UrpPostProcessingController`.
- Use final bloom intensity as `baseIntensityForBloomMode * mobileBloomAmount * skyBrightnessFactor`.
- If mobile quality assets continue to serialize `BloomMode.None`, no bloom should appear.

### Camera post toggle mapping

`CameraConfig.PostEffects` should become a high-level capture post-processing toggle, not a direct component toggle.

When `PostEffects` is false:

- disable capture-only vignette/chromatic/tilt-shift replacement effects
- optionally leave quality bloom on for live XR cameras if that was historically independent

When `PostEffects` is true:

- enable the URP replacements for capture post effects on the relevant camera/profile

Important decision to make during implementation:

- Old `PostEffectsToggle` only toggled `TiltShift` and `Kino.Vignette`, not bloom/FXAA.
- Preserve that behavior initially. Do not make `App:PostProcessing(false)` disable quality bloom unless the UI expects that.

### Capture/dropcam handling

Replace direct component access in `ScreenshotManager` and `DropCamPreviewScreen` with controller calls.

Examples:

- `UrpPostProcessingController.ConfigureScreenshotCamera(camera, enablePostEffects: false)`
- `UrpPostProcessingController.ConfigureDropCamCamera(camera, enableBloom: true, enableCaptureEffects: CameraConfig.PostEffects)`

For mobile:

- Preserve forced HDR-off behavior unless URP mobile testing shows HDR is safe.
- If HDR is off, use bloom only if URP bloom still behaves acceptably in LDR; otherwise keep it disabled for screenshot manager as the old code did.

### RenderWrapper handling

RenderWrapper should be treated as a separate migration item because it mixes several concerns:

- offscreen HDR target rendering
- selection mask generation
- video-recording performance mode
- old post effect feature toggling
- `HDR_EMULATED` keyword selection

First pass:

- Remove `FXAA` and `SENaturalBloomAndDirtyLens` from `RenderWrapper`'s feature tracking after URP post-processing is active.
- Replace `HasHdrDecodePass()` with an explicit HDR encoding policy that does not depend on legacy bloom.
- Keep recording feature-disable behavior by asking `UrpPostProcessingController` to disable camera post-processing during recording.

Second pass:

- Move selection mask generation and any remaining blits into URP render passes if the current `OnRenderImage` path is inert or unstable under URP.

## Implementation Phases

### Phase 1: Establish URP post-processing baseline

1. Create an Open Brush URP post-processing profile asset.
2. Ensure the active camera(s) have `UniversalAdditionalCameraData`.
3. Add or locate a global `Volume` for the main scene, assign the profile, and set it global.
4. Enable `renderPostProcessing` on the main XR camera.
5. Set a visible test bloom intensity temporarily and verify bloom appears in Editor and Android.
6. Reset profile values to conservative defaults after verification.

Verification:

- Use a unique log prefix, e.g. `[OB_URP_POST]`.
- Check `C:/Users/andyb/AppData/Local/Unity/Editor/Editor.log` directly for compile/runtime issues.
- Test with a bright emissive brush stroke and a non-bloom brush.

### Phase 2: Add the URP controller

1. Add `UrpPostProcessingController`.
2. Runtime-instantiate the assigned `VolumeProfile`.
3. Cache `Bloom`, `Vignette`, `ColorAdjustments`, `ChromaticAberration`, and `Tonemapping`.
4. Subscribe to quality and camera post-effect changes.
5. Apply current quality immediately after `QualityControls.Init()`.
6. Add logging for applied quality level, bloom mode, HDR, antialiasing, and post toggle.

Verification:

- Switch quality levels and confirm profile values change.
- Confirm no legacy post component state is required for the URP profile to update.

### Phase 3: Map quality settings

1. Implement `ApplyQuality(AppQualitySettings settings)`.
2. Map `BloomMode.None/Fast/Full/Mobile` to URP bloom values.
3. Map `Fxaa` to URP camera antialiasing.
4. Preserve existing HDR, MSAA, viewport scale, eye texture scale, LOD, and simplification behavior in `QualityControls`.
5. Move mobile bloom fade target out of `MobileBloom` and into the URP controller.

Verification:

- Desktop level 0 has no bloom and no antialiasing.
- Desktop level 1 has lower-cost bloom/AA.
- Desktop level 2/3 has full bloom/AA.
- Mobile quality changes still happen based on FPS/GPU thresholds.
- Mobile bloom remains off while mobile quality assets say `BloomMode.None`.

### Phase 4: Replace capture post effects

1. Replace `PostEffectsToggle` component toggling with URP controller calls.
2. Decide the URP replacements:
   - `Kino.Vignette` -> URP `Vignette`
   - `TiltShift` -> defer unless there is a user-visible capture dependency; URP has no direct built-in tilt-shift equivalent
3. Keep `CameraConfig.PostEffects` and Lua/API behavior unchanged.
4. Update screenshot/dropcam code to use controller APIs instead of legacy component lookups.

Verification:

- Camera panel toggle still persists through `Camera_PostEffects`.
- `App:PostProcessing(false)` changes capture effect state.
- Screenshot and dropcam paths no longer assert about missing legacy bloom components.

### Phase 5: Retire inert legacy components

1. Remove or disable `SENaturalBloomAndDirtyLens`, `FXAA`, `MobileBloom`, `TiltShift`, and `Kino.Vignette` from URP camera prefabs once URP replacements are verified.
2. Remove legacy component lists from `QualityControls`.
3. Remove `RenderWrapper.AddFeature<FXAA>()` and `AddFeature<SENaturalBloomAndDirtyLens>()`.
4. Replace `HasHdrDecodePass()` with an explicit capability flag or remove the HDR-emulated path if obsolete under URP.

Verification:

- Search confirms no production camera depends on legacy `OnRenderImage` post effects.
- Builds do not include unused image effect shaders unless still needed for screenshots/video fallback.

### Phase 6: Mobile optimization fallback if needed

Only do this if URP built-in bloom is too expensive on target hardware.

Create a custom URP `ScriptableRendererFeature` that ports the useful parts of `MobileBloom`:

- central crop bloom
- downsample chain from `BloomLevels`
- optional eye-alternating saved bloom reuse
- sky-brightness bloom fade
- render pass scheduled after transparents/before final post

Do not start here. Built-in URP bloom is less code and easier to maintain; measure first.

## Risks and Open Questions

- `RenderWrapper.OnRenderImage` and `SelectionEffect.OnRenderImage` may already be inert under URP. Selection and capture may require separate URP render passes before post-processing work is complete.
- The old desktop `Fast` vs `Full` distinction is weak in current code because `QualityControls` toggles enablement but does not set `SENaturalBloomAndDirtyLens.lowQuality`.
- URP camera antialiasing support depends on the Unity/URP version and XR path. If FXAA is not available or not suitable in XR, prefer MSAA/SMAA based on actual device results.
- `Packages/manifest.json` still says URP `14.0.12`, while the lock file and Editor log show URP `17.3.0`. Resolve this package metadata mismatch before relying on exact serialized URP fields.
- `CameraConfig.PostEffects` historically controlled capture stylization, not quality bloom. Keep those concepts separate unless product behavior says otherwise.
- Mobile quality assets currently serialize bloom off for every level. If mobile bloom is wanted, update those assets deliberately rather than assuming migration should turn it on.

## Suggested Acceptance Tests

1. Editor desktop:
   - launch main scene
   - verify no `OnRenderImage` legacy post effect is needed for bloom
   - switch quality levels and observe bloom/AA/HDR changes

2. Android XR:
   - verify no flicker with post-processing enabled
   - verify single-pass stereo renders both eyes correctly
   - verify frame time impact of bloom on bright sketches

3. Dynamic mobile quality:
   - force low/high quality thresholds and verify quality changes still update post settings
   - verify bloom fade respects `BloomFadeTime`

4. Capture:
   - screenshot with post effects on/off
   - dropcam preview on mobile
   - video recording path with post disabled during capture if required

5. Regression checks:
   - selection highlight still renders
   - bright emissive brushes still read as bloom-capable
   - non-emissive brushes do not bloom unexpectedly
   - Lua `App:PostProcessing(...)` still controls the intended camera post effects

