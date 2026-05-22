# Combined XR Single-Pass Instanced and URP Post-Processing Migration Plan

## Goal

Move Open Brush to a URP-compatible post-processing stack while enabling OpenXR single-pass instanced rendering, without duplicating shader work or creating conflicting fullscreen/capture paths.

This plan combines:

- XR single-pass instanced shader and project setting migration.
- URP post-processing replacement for legacy `OnRenderImage` and Built-in pipeline command-buffer effects.
- Shared validation for XR, fullscreen blits, bloom, capture cameras, and RenderWrapper behavior.

## Guiding Decisions

- Treat single-pass instanced rendering and URP post-processing as one rendering migration, because both affect fullscreen passes, camera color targets, capture cameras, and per-eye correctness.
- Do not port legacy post effects directly in the first pass. Use URP volumes and `UniversalAdditionalCameraData` first.
- Do not create a custom mobile bloom renderer feature until built-in URP bloom is measured and shown to be too expensive.
- Keep `QualityControls` responsible for non-post quality settings initially. Add a separate URP post-processing controller, then remove legacy component coupling after validation.
- Preserve `CameraConfig.PostEffects` semantics as capture stylization control, not a general quality-bloom toggle.
- Use one shared fullscreen/blit review phase for both XR single-pass correctness and URP post-processing migration.

## Current State Summary

### Already Not Blocking Single-Pass Instanced

- Brush shaders in `open-brush-unity-tools` are URP Shader Graphs and should emit the required XR single-pass code automatically.
- Toon brush shaders already contain URP HLSL subshaders with stereo macros.
- `GpuIntersector.cs` uses a private camera and `RenderTexture` through `Camera.RenderWithShader()`, outside the XR eye-buffer path.
- Legacy shaders in `Assets/Resources/Brushes/` are not used for runtime brush rendering and should not be part of this migration.
- ODS rendering is a separate capture path and should be regression-tested, but it is not the driver for single-pass changes.

### Main Work Areas

- `Assets/Shaders/` hand-written CGPROGRAM shaders need single-pass instancing boilerplate.
- Two environment shaders in the local `open-brush-unity-tools` package need the same instancing fix.
- Legacy post effects using `OnRenderImage` or Built-in pipeline command buffers need URP replacements:
  - `SENaturalBloomAndDirtyLens`
  - `FXAA`
  - `MobileBloom`
  - `TiltShift`
  - `Kino.Vignette`
- `RenderWrapper`, screenshot, dropcam, and Lua/camera post-effect paths need explicit URP-compatible handling.
- Fullscreen/blit shaders need one combined review for stereo-aware sampling and URP render-pass compatibility.

## Phase 0: Baseline and Package Consistency

1. Resolve the URP package metadata mismatch before relying on serialized URP fields:
   - `Packages/manifest.json` references URP `14.0.12`.
   - The lock file and Editor log have shown URP `17.3.0`.
2. Record the active Unity and URP versions.
3. Confirm the manifest points to the local `../open-brush-unity-tools/` package when editing package shaders.
4. Add unique migration log prefixes before runtime checks:
   - `[OB_XR_SPI]` for single-pass instancing checks.
   - `[OB_URP_POST]` for URP post-processing checks.
5. Capture baseline behavior in current multi-pass mode:
   - Brush strokes.
   - UI panels.
   - Controller models and pointer rays.
   - Environment and skybox.
   - Selection outlines.
   - Bloom/FXAA/post effects.
   - Screenshot, dropcam, video, and Lua `App:PostProcessing(...)`.

Verification:

- Search the full Unity Editor log directly for the unique prefixes and errors.
- Do not use arbitrary tail limits that can miss relevant entries.

## Phase 1: Establish URP Post-Processing Baseline

This comes before enabling single-pass instanced so URP post behavior can be validated in the current stereo mode first.

1. Create a baseline URP post-processing profile:
   - `Assets/Settings/PostProcessing/OpenBrushPostProfile.asset`
   - Optional capture-specific profile: `OpenBrushCapturePostProfile.asset`
2. Ensure active cameras have `UniversalAdditionalCameraData`.
3. Add or locate a global `Volume` for the main scene and assign the profile.
4. Enable `renderPostProcessing` on the main XR camera.
5. Add conservative profile components:
   - `Bloom`
   - `Tonemapping`
   - `ColorAdjustments`
   - `Vignette`
   - Optional `ChromaticAberration`
6. Temporarily use an obvious bloom value to verify the URP volume is active in Editor and Android XR.
7. Reset the profile to conservative defaults after verification.

Verification:

- Bright emissive brush strokes can bloom.
- Non-emissive brushes do not bloom unexpectedly.
- No legacy `OnRenderImage` effect is required for visible bloom.
- Editor log contains no compile/runtime errors for `[OB_URP_POST]`.

## Phase 2: Add `UrpPostProcessingController`

Create:

```text
Assets/Scripts/Rendering/UrpPostProcessingController.cs
```

Responsibilities:

- Register the relevant cameras.
- Runtime-instantiate assigned `VolumeProfile` assets before modification.
- Cache URP volume components.
- Apply quality bloom, HDR, antialiasing, and capture post-effect state.
- Subscribe to:
  - `QualityControls.OnQualityLevelChange`
  - `CameraConfig.PostEffectsChanged`
- Provide explicit APIs for screenshot, dropcam, video, and temporary capture overrides.

Initial architecture:

- Keep `QualityControls` as the owner of existing quality-level decisions.
- Let `QualityControls` continue applying MSAA, viewport scale, eye texture scale, LOD, anisotropic filtering, simplification, GPU clock, foveation, and dynamic quality changes.
- Have `UrpPostProcessingController` consume the selected quality settings and apply only URP post/camera-rendering state.

Suggested controller APIs:

```csharp
ApplyQuality(AppQualitySettings settings)
ConfigureScreenshotCamera(Camera camera, bool enablePostEffects)
ConfigureDropCamCamera(Camera camera, bool enableBloom, bool enableCaptureEffects)
SetRecordingPostProcessing(Camera camera, bool enabled)
```

Verification:

- Switching quality levels updates URP profile values.
- The controller does not depend on legacy bloom or FXAA components.
- Logs include applied quality level, bloom mode, HDR, antialiasing, and post-toggle state.

## Phase 3: Map Quality Settings to URP

Keep `AppQualitySettingLevels.BloomMode` as the public quality data model for now.

| Legacy setting | URP behavior |
| --- | --- |
| `BloomMode.None` | Disable bloom or set intensity to `0`. |
| `BloomMode.Fast` | Enable lower-cost bloom. Use conservative intensity and lower quality filtering. |
| `BloomMode.Full` | Enable full-quality bloom values. |
| `BloomMode.Mobile` | Use mobile-tuned bloom values and fade amount. |
| `Hdr = false` | Disable camera HDR and avoid bloom unless explicitly validated in LDR. |
| `Hdr = true` | Enable camera HDR where supported. |
| `Fxaa = true` | Use URP camera antialiasing if available and compatible with XR. Prefer FXAA/SMAA only after device validation. |
| `Fxaa = false` | Disable URP camera antialiasing. |
| `MsaaLevel` | Preserve existing MSAA handling and verify URP pipeline asset behavior. |

Mobile dynamic quality:

- Keep the current FPS/GPU-utilization quality changes in `QualityControls`.
- Preserve `m_DesiredBloom` and fade semantics.
- Move the resulting bloom amount into `UrpPostProcessingController`.
- Compute final mobile bloom as:

```text
baseIntensityForBloomMode * mobileBloomAmount * skyBrightnessFactor
```

- If mobile quality assets still serialize `BloomMode.None`, mobile bloom remains off.

Verification:

- Desktop level 0 has no bloom or AA.
- Desktop level 1 uses lower-cost bloom/AA.
- Desktop level 2/3 use full bloom/AA.
- Mobile dynamic quality changes still occur.
- Mobile bloom remains off while the mobile quality assets request no bloom.

## Phase 4: Enable OpenXR Single-Pass Instanced

Enable single-pass only after URP post-processing has a known baseline in multi-pass.

1. In Unity, set:

```text
Edit -> Project Settings -> XR Plug-in Management -> OpenXR -> Render Mode -> Single Pass Instanced
```

2. This changes:

```text
Assets/XR/Settings/OpenXRPackageSettings.asset
m_renderMode: 0 -> 1
```

3. Commit the settings change separately so it is easy to revert during testing.
4. Run the app with the headset connected or XR Simulation.
5. Record immediately broken rendering by category:
   - Wrong eye.
   - Doubled geometry.
   - Missing geometry.
   - Black or incorrect fullscreen output.

Expected result:

- Shader Graph brush strokes should mostly render correctly.
- UI, controller effects, panels, environment shaders, and fullscreen effects are the likely failure points.

Verification:

- Search the Editor log for `[OB_XR_SPI]`.
- Record before/after CPU draw-call counts when possible, but only after correctness is established.

## Phase 5: Batch-Fix Hand-Written Single-Pass Shaders

Scope:

- `Assets/Shaders/**/*.shader`
- Exclude shaders that are not compiled into runtime paths only after confirming they are unused.

For CGPROGRAM vertex/fragment shaders, add:

```hlsl
#pragma multi_compile_instancing
```

after the existing pragma block, and:

```hlsl
UNITY_TRANSFER_INSTANCE_ID(v, o);
```

immediately after:

```hlsl
UNITY_SETUP_INSTANCE_ID(v);
```

The transfer must be inside the vertex function body so the fragment stage can recover the stereo eye index.

Automation approach:

- Use a scriptable, reviewable edit over `.shader` files.
- Insert `#pragma multi_compile_instancing` once per relevant CGPROGRAM block.
- Insert `UNITY_TRANSFER_INSTANCE_ID(v, o);` after vertex input setup where the output variable is actually `o`.
- Manually review non-standard vertex function names and output variable names.

Surface shaders:

- Add `#pragma multi_compile_instancing`.
- Do not manually add `UNITY_TRANSFER_INSTANCE_ID`; the surface shader compiler generates the required boilerplate.

Manual review list:

- `FlatLit.shader`: geometry shader pass. Confirm whether it is in the main render path, then ensure eye index and instance ID transfer across vertex and geometry stages.
- `ColorPicker_*.shader`: rendered to texture; confirm whether they run in an XR context before adding unnecessary stereo work.
- `GrabHighlightMask.shader` and `GrabHighlightUnmask.shader`: verify URP invokes the stencil/outline path correctly per eye.

Verification:

- Unity compiles after the batch edit.
- UI panels, controller models, pointer rays, teleporter, ground overlays, and selection outlines render correctly in both eyes.

## Phase 6: Fix Local Package Environment Shaders

Edit the local package files:

```text
../open-brush-unity-tools/Packages/open-brush-unity-tools/Runtime/Resources/Environments/Shaders/LinearGradient.shader
../open-brush-unity-tools/Packages/open-brush-unity-tools/Runtime/Resources/Environments/Shaders/ParticleDustBokeh.shader
```

Apply the same CGPROGRAM single-pass fix:

- Add `#pragma multi_compile_instancing`.
- Add `UNITY_TRANSFER_INSTANCE_ID(v, o);` after `UNITY_SETUP_INSTANCE_ID(v);`.

Also verify:

- `ToonMultiMaterial.shader`
- `ToonOutline.shader`

These already contain URP HLSL stereo macros. Add `#pragma multi_compile_instancing` only if practical stereo testing shows they are broken.

Verification:

- Linear gradient environments, skybox, and particle dust render correctly in both eyes.
- Brush Toon modes still render correctly.

## Phase 7: Unified Fullscreen, Blit, and Post-Processing Shader Review

This phase covers both plans' overlapping work. Do it once, after URP post-processing and single-pass instancing are both active.

Review shaders and passes that sample camera or screen textures:

| Shader/path | Review focus |
| --- | --- |
| `BlitDownsample.shader` | `_MainTex_TexelSize` UV behavior under XR. |
| `BlitLinearToGamma.shader` | Simple blit correctness per eye. |
| `BlitLdrPmaOverlay.shader` | Overlay alignment and premultiplied alpha per eye. |
| `BlitWatermark.shader` | Screen-space UV computation. |
| `BlitToCompute.shader` | Whether it is invoked per eye or should remain mono/offscreen. |
| `FixDistortion.shader` | Custom ray math and per-eye UV correction. |
| `FixDistortionAndReveal.shader` | Same as `FixDistortion`. |
| `MobileBloom.shader` | Legacy reference only unless still temporarily active. |
| URP Bloom and final post stack | Confirm URP handles XR texture layout correctly. |
| Capture/dropcam blits | Confirm mono capture paths do not accidentally use XR transforms. |

For shaders that compute screen-space UVs manually, set up stereo eye index in the fragment stage and apply Unity's stereo screen-space transform before sampling screen textures:

```hlsl
UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
uv = UnityStereoTransformScreenSpaceTex(uv);
```

Rules:

- Apply XR screen-space transforms only to XR camera textures.
- Do not apply XR transforms to mono screenshot, dropcam, compute, or private offscreen textures unless the source is actually an XR texture array/slice.
- Prefer URP `ScriptableRenderPass` behavior and URP texture declarations over legacy `tex2D` assumptions when changing active post-processing paths.

Verification:

- Bloom, fade-to-black, watermark, distortion/reveal, and overlays are aligned in both eyes.
- Mono captures are not cropped, doubled, or eye-offset.
- No duplicated shader edits from earlier phases are needed here.

## Phase 8: Replace Capture Post Effects

Replace legacy component toggling with `UrpPostProcessingController` calls.

1. Replace `PostEffectsToggle` behavior:
   - `Kino.Vignette` -> URP `Vignette`.
   - `TiltShift` -> defer unless a current user-visible capture dependency requires a custom replacement.
2. Preserve:
   - `CameraConfig.PostEffects`
   - Lua `App:PostProcessing(true/false)`
   - Global command `ToggleCameraPostEffects`
   - PlayerPref key `Camera_PostEffects`
3. Update screenshot and dropcam paths to use explicit controller APIs instead of finding/disabling legacy bloom components.
4. Preserve mobile screenshot HDR-off behavior unless URP mobile testing proves HDR is safe.

Verification:

- Camera panel post-effect toggle still persists through `Camera_PostEffects`.
- Lua `App:PostProcessing(false)` disables the intended capture effects.
- Screenshot and dropcam paths do not depend on `MobileBloom` or `SENaturalBloomAndDirtyLens`.

## Phase 9: RenderWrapper Migration

Handle `RenderWrapper` after the URP controller and fullscreen review are stable, because it mixes capture, HDR, masks, recording, and old post-effect feature tracking.

First pass:

1. Remove `FXAA` and `SENaturalBloomAndDirtyLens` from render-feature tracking once URP post-processing is active.
2. Replace `HasHdrDecodePass()` with an explicit HDR encoding policy that does not depend on legacy bloom.
3. Preserve recording feature-disable behavior by routing it through `UrpPostProcessingController`.

Second pass:

1. Move selection mask generation and any remaining unstable `OnRenderImage` work into URP render passes if required.
2. Keep offscreen HDR target behavior explicit and separate from camera post-processing state.

Verification:

- Selection highlight still renders.
- Recording paths still disable expensive post effects when required.
- `HDR_EMULATED` is selected by explicit policy, not by legacy component presence.

## Phase 10: Retire Legacy Post-Processing Components

Only after URP replacements are verified:

1. Remove or disable legacy post components from URP camera prefabs:
   - `SENaturalBloomAndDirtyLens`
   - `FXAA`
   - `MobileBloom`
   - `TiltShift`
   - `Kino.Vignette`
2. Remove legacy post component lists from `QualityControls`.
3. Remove legacy post-effect feature registration from `RenderWrapper`.
4. Remove unused legacy image-effect shaders from build inclusion only after confirming no fallback path needs them.

Verification:

- Project search confirms production cameras no longer depend on legacy `OnRenderImage` post effects.
- Editor and Android XR builds have no missing-script or missing-shader warnings.

## Phase 11: Mobile Bloom Fallback, Only If Measured

Do this only if built-in URP bloom is too expensive or visually unsuitable on target hardware.

Create a custom URP `ScriptableRendererFeature` that ports the useful parts of `MobileBloom`:

- Central crop bloom.
- Downsample chain driven by `BloomLevels`.
- Optional eye-alternating saved bloom reuse.
- Sky-brightness bloom reduction.
- Render pass scheduled after transparents and before final post.

This is explicitly a fallback phase. Do not start here.

## Final Acceptance Tests

### Editor Desktop

- Launch the main scene.
- Switch quality levels and confirm bloom, AA, HDR, and post settings update.
- Confirm no legacy `OnRenderImage` post effect is required for bloom.
- Verify UI panels, controllers, selection, fade, and overlays.

### Android XR

- Verify single-pass instanced renders both eyes correctly.
- Verify no flicker or eye mismatch with URP post-processing enabled.
- Verify bright sketches bloom correctly.
- Measure frame time and draw-call changes after correctness is established.

### Dynamic Mobile Quality

- Force low/high quality thresholds.
- Confirm quality changes update URP post settings.
- Confirm bloom fade respects `BloomFadeTime`.
- Confirm mobile bloom remains off when quality assets request `BloomMode.None`.

### Capture and Lua

- Screenshot with capture post effects on and off.
- Dropcam preview on mobile.
- Video recording path with post disabled where required.
- Lua `App:PostProcessing(...)` controls the intended capture effects.

### Regression

- Brush strokes render correctly in both eyes across brush families.
- Toon brush modes still render correctly.
- Environment and skybox render correctly.
- FixDistortion and reveal paths have no eye-swap artifacts.
- ODS rendering still works.
- Mono render textures and private offscreen cameras are not incorrectly treated as XR eye buffers.

## Known Risks and Open Questions

- `RenderWrapper.OnRenderImage` and `SelectionEffect.OnRenderImage` may already be inert under URP; selection and capture may need dedicated URP render passes.
- The old desktop `Fast` vs `Full` bloom distinction is weak because `QualityControls` toggles enablement but does not reliably set `SENaturalBloomAndDirtyLens.lowQuality`.
- URP camera antialiasing support depends on the Unity/URP version and XR path. Device validation decides whether FXAA, SMAA, MSAA, or no post AA is appropriate.
- `CameraConfig.PostEffects` historically controlled capture stylization, not quality bloom. Keep these separate unless product behavior deliberately changes.
- Mobile quality assets currently serialize bloom off at every level. Enabling mobile bloom should be a deliberate quality-data change, not an accidental migration side effect.
