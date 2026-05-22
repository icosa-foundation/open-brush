# Combined XR Single-Pass Instanced and URP Post-Processing Migration Plan

## Goal

Move Open Brush to URP-compatible post-processing and OpenXR single-pass instanced rendering without converting unused legacy shaders, preserving capture/video workflows, and removing legacy Built-in pipeline render hooks deliberately.

This plan covers:

- URP post-processing and camera setup.
- Replacement of legacy `OnRenderImage` and Built-in command-buffer paths.
- OpenXR single-pass instanced enablement.
- Shader usage audit before shader conversion.
- Targeted shader fixes for only referenced runtime shaders.
- Capture, dropcam, video, watermark, selection, and `RenderWrapper` migration.

## Current Codebase Facts

- `com.icosa.open-brush-unity-tools` now points to the local sibling package:
  - `file:../../open-brush-unity-tools/Packages/open-brush-unity-tools`
- `Packages/manifest.json` still lists URP `14.0.12`, while `packages-lock.json` resolves URP/Core/ShaderGraph to built-in `17.3.0`. Resolve this before relying on exact serialized URP fields.
- The active URP asset supports HDR, has MSAA set to `1`, and has no assigned volume profile.
- The URP renderer asset currently has two parked renderer features:
  - `Open Brush Watermark`: disabled.
  - `Open Brush Selection`: disabled.
- URP Render Graph is enabled and compatibility mode is disabled for normal core validation:
  - `Assets/UniversalRenderPipelineGlobalSettings.asset` has `m_EnableRenderGraph: 1`.
  - The serialized `RenderGraphSettings` entry has `m_EnableRenderCompatibilityMode: 0`.
- No serialized `UniversalAdditionalCameraData` or `renderPostProcessing` camera settings were found in `Assets` prefabs/scenes.
- Unity 6000.3.10f1 crashed while opening the project with Composition Layers editor emulation enabled. The Editor log repeatedly showed `Unity.XR.CompositionLayers.Emulation.EmulationColorScaleBiasPass` warnings immediately before the crash.
- `Assets/CompositionLayers/UserSettings/CompositionLayersPreferences.asset` now has Scene and Play Mode emulation disabled to keep the editor open while preserving the Composition Layers package/OpenXR feature for device validation.
- OpenXR render mode is platform-specific:
  - Standalone: `m_renderMode: 0`
  - Android: `m_renderMode: 0`
  - Metro: `m_renderMode: 0`
  - iPhone/WebGL: `m_renderMode: 1`
- `Assets/Shaders` contains 129 shader files. Some are likely unused remnants from pre-URP or old runtime paths.
- 68 project shaders contain `UNITY_SETUP_INSTANCE_ID`.
- 0 project shaders currently contain `UNITY_TRANSFER_INSTANCE_ID`.
- Only 1 project shader currently contains `#pragma multi_compile_instancing`.
- No `#pragma surface` shaders were found under `Assets/Shaders` during this audit.
- Several critical paths still use `OnRenderImage`:
  - `RenderWrapper`
  - `SelectionEffect`
  - `VideoRecorder`
  - `WatermarkEffect`
  - legacy bloom/FXAA/vignette/tilt-shift effects

## Guiding Decisions

- Do not batch-convert every shader until referenced runtime shaders are identified.
- Treat `OnRenderImage` replacement as an early blocker, not cleanup.
- Keep `QualityControls` responsible for quality decisions and non-post quality state at first.
- Add a URP post-processing controller for camera post state, profile state, and capture overrides.
- Preserve `CameraConfig.PostEffects` as capture stylization control, not global quality bloom control.
- Do not port `MobileBloom` immediately. Measure URP bloom first, then decide if a custom renderer feature is required.
- Keep mono offscreen cameras separate from XR eye-buffer logic.
- Match the pre-URP look before adding new URP effects. Do not enable neutral Tonemapping, ColorAdjustments, or other generic URP overrides unless visual comparison proves they are required.

## Phase 0: Configuration and Inventory

1. Confirm package state:
   - `Packages/manifest.json` references local `open-brush-unity-tools`.
   - `Packages/packages-lock.json` source is `local`.
   - Unity can resolve the package after refresh.
2. Resolve URP version metadata:
   - Decide whether the project should declare URP `17.3.0` in `manifest.json`, or whether Unity 6 built-in package resolution intentionally overrides `14.0.12`.
   - Do not serialize new URP fields until this is understood.
3. Record target platforms for this migration:
   - Standalone OpenXR.
   - Android OpenXR.
   - Metro only if still supported.
4. Record the baseline OpenXR settings per target platform.
5. Add unique log prefixes:
   - `[OB_URP_POST]`
   - `[OB_XR_SPI]`
   - `[OB_SHADER_AUDIT]`
   - `[OB_RENDER_HOOKS]`
   - `[OB_PERF]`
6. Search the full Unity Editor log directly for these prefixes and errors during validation.

Crash unblock:

- [x] Checked the full Unity Editor log and Windows Application Error events after the editor crash.
- [x] Confirmed script compilation completed before the crash.
- [x] Identified repeated Composition Layers emulation render-pass warnings in SceneView/GameView rendering.
- [x] Disabled Composition Layers editor emulation:
  - `m_EmulationInScene: 0`
  - `m_EmulationInPlayMode: 0`
- [x] Reopen Unity and confirm the editor no longer crashes.
- [x] Disabled `OpenXRCompositionLayersFeature Standalone` in `Assets/XR/Settings/OpenXRPackageSettings.asset` after emulation render-pass errors continued during Play Mode capture.
- [x] Disabled the serialized `CompositionLayer` component on `Assets/Prefabs/VrSystems/XRRig.prefab` by default; `PassthroughManager` still enables it when `FBPassthrough` is active.
- [x] Let Unity reload scripts after the `UrpPostProcessingController` editor-only emulation unregister guard, then re-enter Play Mode and verify Composition Layers emulation warnings stop for capture. Android Composition Layers support remains enabled for device validation.

## Phase 0.5: Performance Baseline Instrumentation

Status: implemented in this URP branch and in the pre-URP baseline checkout at `C:\Users\andyb\Documents\open-brush-fast`.

Goal: make PC and mobile performance comparisons use the same metric output in both branches before post-processing, Render Graph, or single-pass changes can regress frame rate.

Changes:

- Extended `Assets/Scripts/Debug/ProfilingManager.cs` in both checkouts.
- Existing `TBProfile` summary output is preserved.
- Each profiling run now emits a stable `[OB_PERF] summary ...` line to the Unity log and profiling summary text file.
- Added deterministic HTTP API controls in both checkouts:
  - `profiling.start` with optional mode `standard`, `light`, or `deep`.
  - `profiling.stop`.
  - `profiling.toggle` remains for existing callers, but scripted comparison runs should prefer start/stop to avoid accidental state inversion.
- `ProfilingManager` now exposes `IsProfiling` so the API can make start/stop idempotent instead of relying on assertions.
- The line includes:
  - profile name
  - build/startup string
  - Unity platform
  - mobile hardware flag
  - quality level
  - frame count
  - mean/median/p90/p95/p99/max frame time
  - mean/median FPS
  - percent of frames at or above 90, 75, 72, and 60 FPS
  - batch and triangle counts
  - mobile GPU utilization mean/median/sample count when supported

Usage:

1. Run the same profiling config/sketch on `C:\Users\andyb\Documents\open-brush-fast`.
2. Start capture with `profiling.start` over HTTP, wait the same duration, then call `profiling.stop`.
3. Run the same profiling config/sketch on this URP branch.
4. Use the same `profiling.start` / `profiling.stop` sequence.
5. Search the full Editor or player logs for `[OB_PERF]`.
6. Compare p95/p99 frame time, target-frame-rate percentages, batches, triangles, and GPU utilization.

Acceptance:

- PC URP runs should not materially regress against pre-URP p95/p99 frame time on the same sketch and quality settings.
- Mobile URP runs should not materially reduce `at_or_above_72fps_pct` or `at_or_above_90fps_pct` on the same device/sketch/settings.
- If render-pipeline changes alter batches or triangles, investigate before attributing the delta to post-processing or single-pass.

## Phase 1: Shader Usage Audit Before Conversion

Status: complete for planning. Unity import/compile and headset validation are still pending.

Tracking:

- [x] Inventory project and local package shader files.
- [x] Map shader names to file paths.
- [x] Find material/asset references by shader GUID.
- [x] Find code references through `Shader.Find`, serialized `Shader` fields, `Graphics.Blit`, command buffers, and render hooks.
- [x] Categorize shaders into runtime, capture/offscreen, editor/test, unreferenced/remnant, and unknown dynamic using build-scene, manifest, Resources, asset, and code-path evidence.
- [x] Write `XR-URP-SHADER-AUDIT.md`.
- [x] Feed discoveries back into this plan.

Discoveries so far:

- Static GUID audit found 129 project shader files under `Assets/Shaders`.
- 114 project shaders have at least one static GUID reference.
- 15 project shaders had no static GUID reference.
- `BlitDownsample.shader` is code-only referenced by `RenderWrapper` through `Shader.Find("Hidden/BlitDownsample")`, so it is active despite no GUID reference.
- 14 project shaders are currently deferred as likely unused/remnant until a deeper runtime resource/build-scene check proves otherwise.
- Local package audit found 54 shader/shadergraph files.
- 52 local package shader/shadergraph files have static GUID references.
- Package environment shaders `LinearGradient.shader` and `ParticleDustBokeh.shader` had no static GUID references but remain unknown/dynamic because package environment resources can be loaded by name.
- `ReferenceImageIcon.shader` appears unused in this branch; `Assets/Materials/ReferenceImageIcon.mat` uses `GalleryIcon.shader`.
- `BlitWatermark.shader` appears unused in this branch; active watermark instances serialize `BlitLdrPmaOverlay.shader`.
- Enabled build scenes are `Assets/Scenes/Loading.unity` and `Assets/Scenes/Main.unity`.
- Runtime brushes and environments are manifest-driven through `App.ManifestFull`, `BrushCatalog.LoadBrushesInManifest()`, and `EnvironmentCatalog.LoadEnvironmentsInManifest()`.
- Runtime environments are loaded through `Resources.Load<GameObject>(env.m_EnvironmentPrefab)`, so manifest/resource shader references stay in scope even when not directly present in `Main.unity`.
- `ShaderWarmup` warms materials from `BrushCatalog.m_Instance.AllBrushes`, so package brush shadergraphs referenced by manifest brush materials are runtime scope.
- First-pass project shader conversion scope is now 114 GUID-referenced project shaders plus 1 code-only active shader, `BlitDownsample.shader`.
- First-pass package scope is validation-led: 52 referenced package shaders/shadergraphs are runtime candidates, but Shader Graph assets should only be edited if testing shows a stereo issue.

Current audit artifact:

```text
XR-URP-SHADER-AUDIT.md
```

Goal: identify which shaders are actually referenced by scenes, prefabs, materials, code, Resources, Addressables, and the local package, so single-pass work is targeted.

1. Build a shader reference inventory:
   - Enumerate all `.shader` files under `Assets/Shaders`.
   - Enumerate local package runtime shaders under `../open-brush-unity-tools/Packages/open-brush-unity-tools`.
   - Map shader names to file paths.
2. Find material references:
   - Search `.mat`, `.prefab`, `.unity`, `.asset`, and ScriptableObjects for shader GUIDs.
   - Include renderer feature assets, volume/profile assets, quality assets, and Resources.
3. Find code references:
   - Search `Shader.Find(...)`.
   - Search serialized `Shader` fields.
   - Search `new Material(...)`, `Graphics.Blit(...)`, command buffers, `RenderWithShader(...)`, and renderer feature setup.
4. Categorize each shader:
   - **Runtime referenced**: used by scene, prefab, material, Resources, code, or package runtime.
   - **Capture/offscreen only**: screenshot, video, dropcam, icon generation, ODS, intersector, thumbnails.
   - **Editor/test only**: editor utilities, previews, tests.
   - **Unreferenced/remnant**: no current reference found.
   - **Unknown dynamic**: possible runtime use through string names or dynamically loaded content.
5. Create a shader audit artifact:
   - Suggested file: `XR-URP-SHADER-AUDIT.md`
   - Include counts, categories, and file lists.
6. Only convert **runtime referenced**, **capture/offscreen**, and **unknown dynamic** shaders unless testing proves an unreferenced shader is still loaded.
7. Skip the 14 deferred project shaders listed in `XR-URP-SHADER-AUDIT.md` during the first conversion pass.

Verification:

- `XR-URP-SHADER-AUDIT.md` explains why each conversion candidate is in scope.
- Unused/remnant shaders are not modified in the first pass.
- Unity reload/compile is still required before treating the audit as runtime-validated.

## Phase 2: URP Camera and Volume Baseline

This establishes a working URP post-processing path before single-pass is enabled.

Status: in progress.

Tracking:

- [x] Located enabled runtime scenes: `Assets/Scenes/Loading.unity` and `Assets/Scenes/Main.unity`.
- [x] Added `Assets/Scripts/Rendering/UrpPostProcessingController.cs`.
- [x] Attached `UrpPostProcessingController` to the `App` object in `Assets/Scenes/Main.unity`.
- [x] Added named baseline profile assets:
  - `Assets/Settings/PostProcessing/OpenBrushPostProfile.asset`
  - `Assets/Settings/PostProcessing/OpenBrushCapturePostProfile.asset`
- [x] Runtime controller creates a global URP `Volume` with only known legacy-equivalent post components: Bloom and Vignette.
- [x] Runtime controller ensures `UniversalAdditionalCameraData` exists on loaded cameras.
- [x] Runtime controller enables post-processing on main/non-capture cameras and leaves capture/offscreen cameras disabled by default.
- [x] Removed Tonemapping and ColorAdjustments from the Phase 2 baseline because they were not identified as pre-URP production effects.
- [x] Disabled serialized legacy post-processing components in `Assets/Scenes/Main.unity`:
  `SENaturalBloomAndDirtyLens`, `MobileBloom`, `Kino.Vignette`, `PostEffectsToggle`, and `TiltShift`.
- [x] Disabled serialized legacy `SENaturalBloomAndDirtyLens` and `FXAA` components in `Assets/Prefabs/VrSystems/XRRig.prefab`.
- [x] Disabled serialized legacy bloom/FXAA components in capture-related prefabs: `Assets/Prefabs/VrSystems/ODS.prefab` and `Assets/ThirdParty/LIV/LIV Camera.prefab`.
- [x] Stopped `QualityControls`, `RenderWrapper`, `PostEffectsToggle`, and `DropCamPreviewScreen` from re-enabling legacy post-processing.
- [x] Removed legacy post component lists and mobile bloom fade toggling from `QualityControls`.
- [x] Added a runtime legacy post disable guard in `UrpPostProcessingController` using `[OB_URP_POST]` logging.
- [x] Added an editor-only guard in `UrpPostProcessingController` to unregister Unity Composition Layers emulation render passes during editor validation.
- [x] Enabled URP post-processing for editor-generated brush screenshot PNGs in `Assets/Editor/UiScreenshotter.cs` so brush captures can be compared against old post-effect output.
- [x] Let Unity reload scripts and verify compile/import after these Phase 2 edits.
- [ ] Search the full Unity Editor log for `[OB_URP_POST]` after entering Play Mode.
- [x] Confirm visible URP bloom can be made visible with the current baseline values.
- [ ] Complete capture path validation checklist.

Capture path validation checklist:

- [x] Still image snapshot PNG: HTTP/API path renders with and without capture post-processing; retest visual bloom against old post effects.
- [x] GIF snapshots: HTTP/API Auto GIF and Time GIF paths render files after inactive-tool coroutine/task cleanup fixes; retest visual bloom against old post effects.
- [x] Save-icon/sketch thumbnail generation: HTTP validation renders `saveicon.png`; inspected output is nonblank and not magenta.
- [x] Dropcam preview/still camera: HTTP validation renders `dropcam.png`; inspected output is nonblank and not magenta.
- [x] Video recorder camera output: HTTP validation renders a post-processed PNG frame sequence through the video camera path.
- [ ] Auto GIF camera: output file generation works through HTTP API; verify bloom behavior and frame orientation visually.
- [ ] Time GIF camera: output file generation works through HTTP API; verify bloom behavior and frame orientation visually.
- [x] ODS/360 capture: HTTP validation renders `snapshot360.png_000000.png`; inspected output is nonblank and not magenta.
- [x] LIV camera path: intentionally ignored for this pass.
- [x] Lua/API capture parity: Lua now exposes positioned snapshot post-processing control plus Auto GIF, Time GIF, DropCam snapshot, save-icon snapshot, and video capture helpers matching the HTTP capture API surface. Runtime capture validation is deferred until after Phase 3.
- [x] HTTP API validation snapshot: `capture.snapshot` creates post-processed and no-post PNG files in the Snapshots folder.
- [x] HTTP API GIF validation: `capture.autogif` and `capture.timegif` create GIF files in the Snapshots folder.
- [ ] Visual capture comparison: deferred; inspect generated PNG/GIF/video outputs against pre-URP captures for bloom strength, orientation, framing, alpha/background behavior, and stereo/mono slicing.
- [ ] Editor brush screenshot PNGs: verify bloom is present after the `UiScreenshotter` opt-in change.
- [x] Search Unity console after capture passes for `[OB_URP_POST]`, `[OB_URP_CAPTURE]`, `[OB_URP_CAPTURE_API]`, shader/material errors, render texture errors, and capture exceptions. Full `Editor.log` is currently polluted with null-filled content, so MCP console checks were used for fresh-run validation.
- [x] Resolve Composition Layers emulation render-pass errors during capture before treating capture validation as clean.

Discoveries:

- There was already an empty `Assets/DefaultVolumeProfile.asset` used by URP global settings, but the Open Brush baseline now uses separate named profiles to avoid coupling app behavior to Unity's default profile.
- The new profile assets are intentionally empty on disk for now; `UrpPostProcessingController` runtime-instantiates them and adds baseline volume components to the clones. This avoids fragile manual YAML serialization of URP volume overrides.
- Phase 2 should not introduce Tonemapping or ColorAdjustments. The pre-URP codebase had explicit bloom and vignette paths, but no confirmed equivalent for those two effects.
- Capture/offscreen camera classification currently uses target textures, `VideoRecorder`, and camera names containing `screenshot`, `drop`, `video`, `gif`, `saveicon`, or `capture`. This should be tightened as individual capture paths are migrated.
- Serialized verification now shows zero enabled targeted legacy post components in `Main.unity` and zero enabled legacy bloom/FXAA components in the XR rig, ODS, and LIV camera prefabs.
- `MobileBloom` was already serialized disabled in `Main.unity`; the current change also prevents runtime code from enabling it on dropcam previews.
- Brush screenshot generation intentionally opts into URP post-processing and uses an HDR source render target before writing the final PNG, while environment and panel screenshot generation keep their previous default behavior.
- `TaperedMarker_Flat` and `ThickGeometry` rendered magenta because their brush descriptors still referenced legacy Built-in project materials. They now reference existing URP package materials from the local `open-brush-unity-tools` package.
- Still image and GIF snapshots rendered correctly but lacked bloom. `ScreenshotManager.RenderToTexture()` now has an explicit URP post-processing opt-in, and snapshot, Auto GIF, and Time GIF capture paths use it with an HDR intermediate target.
- Capture post-processing opt-in logs with `[OB_URP_CAPTURE]` for the first few capture renders to make API-driven validation searchable in `Editor.log`.
- HTTP validation should use direct API commands rather than a Lua/plugin bridge. Added `capture.snapshot` with filename, width, height, supersampling, and post-processing arguments; it writes to the Snapshots folder and logs `[OB_URP_CAPTURE_API]`.
- Added `capture.autogif` and `capture.timegif` HTTP commands so GIF capture validation can be run repeatably in non-VR mode without driving controller/tool input manually.
- First HTTP GIF validation attempt found the `MultiCamTool` GameObject can be inactive in non-VR/API-driven mode, so its own `StartCoroutine` calls fail. The API helpers now start GIF capture coroutines through the active `App` behaviour.
- Second HTTP GIF validation attempt showed Auto GIF writes the file, but inactive `MultiCamTool` does not run its usual `UpdateTool()` completion polling, so GIF state remained busy and blocked Time GIF. API GIF coroutines now wait for `GifEncodeTask` completion and call the existing completion cleanup directly.
- HTTP API examples and fallback filenames now use generic capture names (`snapshot.png`, `autogif.gif`, `timegif.gif`) rather than migration-specific names.
- Added validation-only HTTP endpoints for non-VR capture smoke tests:
  - `capture.saveicon`
  - `capture.dropcam`
  - `capture.video`
  - `capture.360`
- HTTP capture endpoints that can reasonably vary post-processing now accept an explicit post-processing argument and default to `CameraConfig.PostEffects` when omitted: `capture.snapshot`, `capture.autogif`, `capture.timegif`, `capture.dropcam`, and `capture.video`.
- Lua capture helpers now provide parity for the same capture workflows: `App:TakeSnapshot(...)`, `App:TakeAutoGif(...)`, `App:TakeTimeGif(...)`, `App:TakeDropCamSnapshot(...)`, `App:TakeSaveIconSnapshot(...)`, `App:TakeVideo(...)`, and existing `App:Take360Snapshot(...)`.
- Latest HTTP validation outputs:
  - Snapshot with post-processing: `C:\Users\andyb\Documents\Open Brush\Snapshots\snapshot.png`.
  - Snapshot without post-processing: `C:\Users\andyb\Documents\Open Brush\Snapshots\snapshot_nopost.png`.
  - Auto GIF: `C:\Users\andyb\Documents\Open Brush\Snapshots\autogif_phase2_validation.gif`.
  - Time GIF: `C:\Users\andyb\Documents\Open Brush\Snapshots\timegif_phase2_validation.gif`.
  - Save icon: `C:\Users\andyb\Documents\Open Brush\Snapshots\saveicon.png`.
  - Dropcam: `C:\Users\andyb\Documents\Open Brush\Snapshots\dropcam.png`.
  - Video frame sequence: `C:\Users\andyb\Documents\Open Brush\Videos\video_frames\video_frame_000001.png` through `video_frame_000012.png`, with metadata `C:\Users\andyb\Documents\Open Brush\Videos\video_sequence.txt`.
  - ODS/360: `C:\Users\andyb\Documents\Open Brush\Snapshots\snapshot360.png_000000.png`.
- Pre-URP behavior check in `C:\Users\andyb\Documents\open-brush-fast`: `SaveIconCamera` had `SENaturalBloomAndDirtyLens` directly enabled and no `PostEffectsToggle`, so save icons/thumbnails effectively always included bloom. User-facing snapshot, GIF, Lua snapshot, and still-frame video captures now use `CameraConfig.PostEffects`, preserving the `Flags.PostEffectsOnCapture` / `Camera_PostEffects` control path.
- ODS/360 API capture needed the full ODS camera parent hierarchy activated before starting `HybridCamera.Render`; otherwise Unity reported `Coroutine couldn't be started because the game object 'ODSCamera' is inactive`.
- New ODS/360 post-processing finding: `HybridCamera.Render` still searches enabled behaviours for a public `OnRenderImage` method and invokes it via reflection before final PNG write. In the URP branch that resolves to `RenderWrapper.OnRenderImage`, which now correctly pass-throughs under SRP. That keeps 360 captures nonblank but means they should not be counted as URP-bloom/post-processed until a dedicated ODS URP composite is added or the old behaviour is intentionally retired.
- Unity live console still reported `Unity.XR.CompositionLayers.Emulation.EmulationColorScaleBiasPass` `Execute is not implemented` errors during `ScreenshotManager.RenderToTexture()` calls after disabling the Standalone OpenXR feature and the loaded `CompositionLayer` component. The package registers the pass once the Composition Layers manager starts, so `UrpPostProcessingController` now explicitly unregisters the package emulation render passes in the editor. After removing an over-restrictive passthrough early return and reloading scripts, HTTP snapshot, Auto GIF, and Time GIF regenerated without Composition Layers errors.
- Remaining console noise in the latest capture retest is unrelated to the URP capture path: Photon `DontDestroyOnLoad` root-object usage, an expired Viverse token warning, one Canvas transform warning, and a WindowsMediaFoundation video color-primaries warning.

1. Locate the actual XR camera prefabs/scene objects and capture cameras:
   - Main XR camera.
   - Screenshot camera(s).
   - Dropcam camera.
   - Video recorder camera.
   - Any Lua/API camera paths.
2. Add or ensure `UniversalAdditionalCameraData` for these cameras.
3. Create a baseline profile:
   - `Assets/Settings/PostProcessing/OpenBrushPostProfile.asset`
   - Optional: `OpenBrushCapturePostProfile.asset`
4. Add a global Volume for the main scene or assign a default URP volume profile intentionally.
5. Enable `renderPostProcessing` only on cameras that should participate.
6. Add only legacy-equivalent URP components:
   - `Bloom`
   - `Vignette`, initially inactive except where replacing old `Kino.Vignette`/capture post-effects behavior.
7. Do not add Tonemapping, ColorAdjustments, ChromaticAberration, or other effects during the baseline matching pass.
8. Temporarily test visible bloom, then reset to conservative defaults.

Verification:

- URP bloom can be made visible without `SENaturalBloomAndDirtyLens`.
- Non-emissive strokes do not bloom unexpectedly.
- Capture cameras with target textures still render normally.
- A/B comparison against the pre-URP look shows no added tonemapping, color, contrast, saturation, or vignette shift from the baseline itself.

## Phase 3: Replace Legacy Render Hooks Early

These are blockers for a real URP migration because URP does not support the old Built-in post-effect model reliably.

Status: implementation mostly complete at compile/registration level. Selection outline is parked as a known URP visual bug; other render-hook cleanup continues.

### RenderWrapper

1. Separate responsibilities:
   - HDR target policy.
   - MSAA/render-scale handling.
   - recording performance mode.
   - selection mask production.
   - legacy post-effect feature toggling.
2. Remove `FXAA` and `SENaturalBloomAndDirtyLens` from feature tracking once URP equivalents are active.
3. [x] Replace `HasHdrDecodePass()` with explicit HDR encoding policy.
4. [x] Route recording post-processing disablement through the new URP controller/capture camera policy.
5. [x] Decide whether desktop offscreen rendering still needs `m_camCopy.Render()` under URP or should move to a renderer feature/render pass.

Progress:

- Replaced the `SENaturalBloomAndDirtyLens`-based HDR decode check with `ShouldEncodeHdrToLdr(RenderTextureFormat fmt)`.
- In URP, `HDR_EMULATED` is now disabled for ARGB32 targets because there is no legacy image-effect decode pass later in the camera chain.
- The Built-in fallback path can still enable alpha-exponent encoding for ARGB32 targets.
- In URP, `OnPreRender()` now skips the desktop camera-copy render into `m_hdrTarget`; that render target was a Built-in workaround and is no longer consumed by the production URP path.
- `RenderWrapper.OnPreCull()` still keeps the readback callback timing used by the GPU intersector. `GpuIntersector` also has a URP `beginCameraRendering` fallback, so this remains guarded against duplicate dispatch in one frame.
- `RenderWrapper.OnRenderImage()` now pass-throughs immediately under SRP so the Built-in mask/HDR blit path cannot run in URP compatibility mode.

### SelectionEffect

1. [~] Replace `OnRenderImage` outline composition with a URP renderer feature/pass for desktop selection highlights.
2. [x] Keep mobile shader-based selection behavior separate.
3. [x] Preserve the existing selection mask semantics by keeping the same stencil ref, mask extraction shader, blur passes, and composite shader.

Progress:

- Added `Assets/Scripts/Rendering/Selection/UrpSelectionRendererFeature.cs`.
- Registered `Open Brush Selection` on `Assets/Settings/Open Brush Universal Render Pipeline Asset_Renderer.asset` at `AfterRenderingPostProcessing`.
- The URP pass currently draws registered highlight meshes into an RFloat mask using `Hidden/UrpSelectionMask`, then runs the existing `SelectionPostEffect`/`SelectionPostEffectAlt` downsample, blur, and outline-composite passes.
- `SelectionEffect` now exposes the minimal state/material API needed by the URP pass while leaving the Built-in `OnRenderImage` path intact as fallback.
- Added play-mode/initialization guards after Unity reported the renderer feature can query `SelectionEffect` before `Start()` initializes the requested-mesh list.
- After runtime feedback that no selection outline appeared, removed the Built-in-only pre-cull/pose gate from the URP pass and made the renderer feature fall back to `App.Instance.SelectionEffect` when the rendering camera does not carry `SelectionEffect` directly. This is needed for non-VR/Game View camera paths that still route selection highlight requests through the app-level selection effect.
- Selection mask extraction now uses a camera-sized mask target. The URP path no longer depends on reading the camera stencil buffer; it draws registered highlight meshes directly into an RFloat mask using `Hidden/UrpSelectionMask`, then runs the existing blur/composite shader passes.
- The URP selection pass explicitly binds `_MainTex` before each legacy post-effect pass because `Blitter.BlitCameraTexture` does not reliably feed shaders that sample `_MainTex` instead of URP's blit texture names.
- Temporary `[OB_URP_SELECTION]` instrumentation confirmed selection meshes register, the pass enqueues, `Execute` runs, and one submesh is drawn each frame on the active camera path. The outline is still not visually composited, so the bug is parked for now at user request.
- Temporary `[OB_URP_SELECTION]` logging was removed after diagnosis to avoid polluting the editor log.
- `Open Brush Selection` renderer feature is disabled while the visual bug is parked, because the pass currently incurs per-frame work without producing a visible outline.
- `SelectionEffect.OnRenderImage()` now pass-throughs immediately under SRP; the Built-in selection composite path remains fallback-only.
- Mobile selection remains on the existing shader-global path and is not routed through the desktop URP pass.
- Compile-level validation passed after script reload; runtime visual validation is deferred until all Phase 3 render-hook replacements are in place.

New discoveries:

- The old desktop path was stencil-driven: `GrabHighlightMask` writes stencil ref 1 with `ColorMask 0`, `StencilToMask` converts that stencil to a mask texture, and the selection post-effect composites an inflated outline over the camera color.
- The first URP attempt tried to preserve the old stencil contract, but runtime feedback showed no visible outline. The current parked implementation bypasses stencil and writes a direct mesh mask; the remaining failure is likely in fullscreen composite/shader expectations or camera target binding.

Selection bug notes, parked:

- Symptom: desktop selection outline is not visible under URP.
- User retested after both the stencil-preserving URP pass and the direct mask pass; neither produced a visible outline.
- Temporary `[OB_URP_SELECTION]` instrumentation confirmed the upstream selection path is alive:
  - `RegisterMesh` fired repeatedly for the selected stroke batch mesh, e.g. `Batch_0_f1114e2e-eb8d-4fde-915a-6e653b54e9f5`.
  - `ShouldRender=true` was reached with `SelectionPostEffectAlt`.
  - `AddRenderPasses enqueue` fired for `SceneCamera` and later `Camera (eye)`.
  - `Execute` ran with camera targets such as `700x613` for SceneCamera and `2112x2304` for `Camera (eye)`.
  - `DrawMeshes` reported `requested=1 drawn=1`.
- This rules out the obvious failures:
  - selection requests are not missing;
  - `SelectionEffect` material references are not null;
  - the renderer feature is not missing from the renderer asset;
  - the pass is not being skipped;
  - the mesh draw command is being issued.
- Current likely failure area:
  - the mask draw may not be writing meaningful pixels because the selected batch mesh/material geometry path needs additional vertex streams, transforms, or shader state not covered by the simple `Hidden/UrpSelectionMask` shader;
  - or the mask texture is valid but the legacy `SelectionPostEffectAlt` fullscreen composite is not sampling/compositing correctly in URP/XR target layout;
  - or the active visible camera path differs from the camera where the pass is compositing for part of the non-VR/SceneView/XR stack.
- Current code state:
  - `UrpSelectionRendererFeature` exists but `Open Brush Selection` is disabled in `Assets/Settings/Open Brush Universal Render Pipeline Asset_Renderer.asset`.
  - `Hidden/UrpSelectionMask` exists as the direct mask attempt.
  - `SelectionEffect.OnRenderImage()` is SRP pass-through, so the Built-in selection composite no longer runs under URP compatibility mode.
  - Temporary `[OB_URP_SELECTION]` logging has been removed to keep the editor log usable.
- Recommended resume steps:
  1. Add an explicit debug mode to `UrpSelectionRendererFeature` that composites the raw mask texture directly to the camera color target as white/red, bypassing blur and `SelectionPostEffectAlt`.
  2. If raw mask is blank, replace `Hidden/UrpSelectionMask` with a pass that uses the same vertex input conventions as the selected stroke batch shaders, or draw via the original `GrabHighlightMask` material into a color target instead of stencil.
  3. If raw mask is visible, port `SelectionPostEffectAlt` to a URP-native fullscreen shader that samples URP blit texture names and handles XR texture layout explicitly.
  4. Validate in Game View/non-VR first, then on `Camera (eye)`, then with XR single-pass/multipass.

### VideoRecorder

1. [x] Replace `OnRenderImage` frame capture with a URP-compatible capture point.
2. Preserve compute-buffer blit path using `BlitToCompute` or a URP pass equivalent.
3. Verify playback and capture paths independently.

Progress:

- URP video capture now routes through the existing `StillFrameSequenceExporter` path by default, because it renders explicitly through `ScreenshotManager.RenderToTexture()` instead of depending on `VideoRecorder.OnRenderImage`.
- The direct ffmpeg/compute-buffer `VideoRecorder` path remains available for the Built-in pipeline, where `OnRenderImage` is valid.
- A later cleanup can either remove the direct URP ffmpeg path entirely or port it to a renderer pass, but production URP capture no longer depends on the Built-in image-effect hook.
- `VideoRecorder.OnRenderImage()` now pass-throughs immediately under SRP to keep the direct ffmpeg/compute-buffer path Built-in-only.

### Watermark

1. [x] Replace `WatermarkEffect.OnRenderImage` with a URP overlay/pass or explicit capture-composite step.
2. Preserve `CameraConfig.Watermark` and Lua/API behavior.

Progress:

- Added `Assets/Scripts/Rendering/UrpWatermarkRendererFeature.cs`.
- Registered `Open Brush Watermark` on `Assets/Settings/Open Brush Universal Render Pipeline Asset_Renderer.asset` at `AfterRenderingPostProcessing`.
- Converted `WatermarkEffect` into the camera-local settings/material provider for the URP pass.
- Left `WatermarkEffect.OnRenderImage` as a Built-in render pipeline fallback only; when an SRP is active it performs a pass-through blit.
- Added a `RecordRenderGraph` implementation to `UrpWatermarkRendererFeature` using URP `UniversalResourceData` and `RenderGraphUtils.AddBlitPass`; the compatibility-mode `Execute` path remains for the current project settings.
- Compile-level validation passed after script reload. Runtime visual validation is intentionally deferred until Phase 3 replacements are complete.

Verification:

- Selection outline renders on desktop.
- Video recording captures frames.
- Watermark toggles correctly.
- Rendering no longer depends on legacy `OnRenderImage` for production paths.

## Phase 3.5: Render Graph Readiness and Compatibility Mode Removal

Status: complete for normal non-XR core validation. Selection, watermark, ODS/360, XR desktop, and Android validation are parked/deferred and excluded from this completion gate.

Goal: remove URP compatibility mode deliberately after legacy Built-in render hooks and unsafe command-buffer/fullscreen paths have been migrated.

Current state:

- `Assets/UniversalRenderPipelineGlobalSettings.asset` has `m_EnableRenderGraph: 1`.
- `RenderGraphSettings` has `m_EnableRenderCompatibilityMode: 0`.
- `Open Brush Selection` renderer feature remains disabled.
- `Open Brush Watermark` renderer feature is disabled for this pass because watermark is parked.
- ODS/360 validation is parked.

Prerequisites:

- Production `OnRenderImage` paths are replaced or isolated:
  - `RenderWrapper`
  - `SelectionEffect`
  - `VideoRecorder`
  - `WatermarkEffect`
- Mobile bloom is retired in favor of URP bloom for SRP runs.
- Active fullscreen/copy/composite operations are implemented through URP-compatible renderer features or explicit capture steps.
- Capture/offscreen cameras have documented mono-vs-XR texture handling.
- `[OB_PERF]` baseline data exists for pre-URP and compatibility-mode URP runs.

Work:

1. Audit active `CommandBuffer` use:
   - `MobileBloom`
   - selection mask/intersection paths
   - video capture/readback paths
   - screenshot/dropcam/icon generation paths
2. Replace unsafe `Graphics.Blit`/fullscreen assumptions in active URP paths with renderer-pass or Render Graph-compatible equivalents.
3. Enable Render Graph in a separate change:
   - `m_EnableRenderGraph: 1`
   - `m_EnableRenderCompatibilityMode: 0`
4. Run SceneView/GameView smoke tests before entering Play Mode.
5. Run Play Mode without XR, then XR desktop, then Android.
6. Compare `[OB_PERF]` before/after compatibility-mode removal.

Verification:

- Editor opens without SceneView/GameView render-loop warnings escalating to crashes.
- Main scene renders with Render Graph enabled.
- Normal core paths work with parked exclusions documented:
  - main scene rendering
  - brush drawing
  - brush switching, color changes, and material changes
  - erase/select basics excluding the parked selection outline visual
  - save/load sketch
  - normal snapshot, GIF, save icon, dropcam, and still-frame video capture with post-processing
  - HTTP/Lua capture post-processing controls
- XR desktop and Android render both eyes correctly.
- `[OB_PERF]` p95/p99 frame time and target-frame-rate percentages do not materially regress versus compatibility mode.

Progress:

- Audited active render-hook/fullscreen paths:
  - `MobileBloom` used Built-in `CameraEvent` command buffers and `BuiltinRenderTextureType.CameraTarget`.
  - `GpuIntersector` uses explicit command-buffer execution plus a URP `beginCameraRendering` readback trigger and is already isolated from `Camera.RenderWithShader`.
  - `ScreenshotManager` still uses explicit `Camera.Render()`/`RenderWithShader()` for capture paths; this is acceptable before Render Graph removal but remains in the capture audit.
  - `SaveIconTool.ProgrammaticCaptureSaveIcon` does one direct save-icon camera render before calling `ScreenshotManager.RenderToTexture(..., includePostProcessing: true)`. The final saved icon path is explicitly post-processed and matches the pre-URP always-bloom behaviour; the preliminary render is still undocumented and should be smoke-tested but does not appear to be the final icon write.
  - `SketchControlsScript.DoProfiling` creates a temporary profiling screenshot camera and calls `Camera.Render()` into a local render texture. This is diagnostic capture only, not a production post-processing hook.
  - `OdsDriver` and `UnityODS` call `Camera.Render()`/`RenderToCubemap()` repeatedly for 360 capture, then composite with `Graphics.DrawTexture`/`Graphics.Blit`. This is an explicit capture pipeline, not the main camera render loop, but it still uses legacy post-processing discovery via reflected `OnRenderImage` and needs a separate URP-post decision.
  - `OverrideCameraFramerate` disables its local camera and manually calls `Camera.Render()` on an interval. This is a legacy helper; no current references were found in the static audit, so it should be treated as inactive until a scene/prefab reference proves otherwise.
  - `ReferenceVideo` and `GpuTextRender` use local render-texture blits, not camera render-loop hooks.
- Added a hard SRP guard to `MobileBloom`: under any active Scriptable Render Pipeline it disables itself before creating or attaching Built-in command buffers.
- Removed URP-era assertion noise from screenshot/dropcam startup checks that expected legacy `MobileBloom`/`SENaturalBloomAndDirtyLens` components. Those assertions now only apply to the Built-in pipeline fallback.
- Isolated remaining `OnRenderImage` methods under SRP:
  - `RenderWrapper.OnRenderImage()` pass-throughs under SRP.
  - `SelectionEffect.OnRenderImage()` pass-throughs under SRP.
  - `VideoRecorder.OnRenderImage()` pass-throughs under SRP.
  - `WatermarkEffect.OnRenderImage()` was already SRP pass-through.
- `Open Brush Selection` renderer feature is disabled until the parked composite bug is fixed.
- Flipped Render Graph settings for normal core validation:
  - `m_EnableRenderGraph: 1`
  - `m_EnableRenderCompatibilityMode: 0`
- Disabled `Open Brush Watermark` for this pass because watermark is parked.
- Unity import/script reload after the flip completed with no console errors or warnings.
- Renderer feature check confirms:
  - `Open Brush Watermark`: inactive.
  - `Open Brush Selection`: inactive.
- Non-XR Play Mode smoke start/stop completed with no console errors or warnings. This only validates editor/play-mode startup, not drawing, capture outputs, or performance.
- First API smoke after the Render Graph flip exposed two non-Render-Graph findings:
  - Calling `new` during the automated smoke can trip a destroyed `LuaManager` reference while panels are being rebuilt; removed `new` from the automated Render Graph smoke sequence.
  - `ScreenshotManager.SaveToMemory` asserted that every saved render texture was `ARGB32`, but post-processed captures intentionally render through `ARGBFloat`. Removed the stale assertion; PNG save/readback already handles the texture through `ReadPixels`.
- Narrow non-XR API smoke with Render Graph on completed after the assertion fix and without Unity console errors or warnings:
  - drew an API `ink` path after setting brush size and color;
  - emitted `[OB_PERF] summary ...` for the profiling start/stop path;
  - wrote `C:\Users\andyb\Documents\Open Brush\Snapshots\snapshot_rendergraph_3.png` at 640x360;
  - wrote `C:\Users\andyb\Documents\Open Brush\Snapshots\saveicon_rendergraph_3.png` at 256x256;
  - wrote `C:\Users\andyb\Documents\Open Brush\Snapshots\dropcam_rendergraph_3.png` at 640x360;
  - wrote `C:\Users\andyb\Documents\Open Brush\Videos\video_rendergraph_3_frames\video_rendergraph_3_frame_000001.png` and companion sequence metadata for the still-frame video path.
- Auto GIF and Time GIF smoke also passed with Render Graph on and no current Unity console errors or warnings:
  - `C:\Users\andyb\Documents\Open Brush\Snapshots\autogif_rendergraph_3.gif`, 20 frames.
  - `C:\Users\andyb\Documents\Open Brush\Snapshots\timegif_rendergraph_3.gif`, 50 frames.
- Follow-up editor-log check found a normal video tool state bug after URP still-frame video capture:
  - `MultiCamTool.StopVideoCapture()` and the capture timer only consulted `VideoRecorderUtils.ActiveVideoRecording`, but URP uses `ActiveStillFrameExporter`, so the tool could remain in `Capturing` state after the exporter had stopped.
  - The stale state repeatedly called `FileUtils.HasFreeSpace()` with an invalid recorder path and then produced repeated null-reference errors in `UpdateVideoCaptureState()`.
  - Added shared active-capture accessors to `VideoRecorderUtils`, exposed frame count/FPS/path from `StillFrameSequenceExporter`, and changed `MultiCamTool` to use the shared capture state for timer display, stop, and free-space checks.
  - Script reload completed with no Unity console errors or warnings.
  - Narrow `capture.video=video_rendergraph_4.mp4,0.5,true` smoke wrote:
    - `C:\Users\andyb\Documents\Open Brush\Videos\video_rendergraph_4_frames\video_rendergraph_4_frame_000001.png`
    - `C:\Users\andyb\Documents\Open Brush\Videos\video_rendergraph_4_frames\video_rendergraph_4_frame_000002.png`
    - `C:\Users\andyb\Documents\Open Brush\Videos\video_rendergraph_4_frames\video_rendergraph_4_frame_000003.png`
    - `C:\Users\andyb\Documents\Open Brush\Videos\video_rendergraph_4_sequence.txt`
  - Unity console stayed clean after the smoke, and the editor log has no new `HasFreeSpace`, `NullReferenceException`, or frame-capture errors after the `video_rendergraph_4` completion marker.
- Fresh Render Graph `[OB_PERF]` smoke completed through the HTTP API with valid brush commands and no Unity console errors or warnings:
  - `frames=60 mean_ms=101.11 median_ms=101.06 p95_ms=102.50 mean_fps=9.9 median_fps=9.9 batches=1 tris=32`.
  - The editor was not a controlled performance environment, so treat this as instrumentation smoke only, not a pass/fail performance comparison.
  - Fixed the profiling API label path in both this URP branch and the pre-URP baseline checkout at `C:\Users\andyb\Documents\open-brush-fast`:
    - `profiling.start=standard,label` now treats `standard` as the profiling mode and `label` as the active profile label.
    - Existing `profiling.start=standard` behavior remains valid.
    - The active label is scoped to the profiling run and does not permanently mutate `App.UserConfig.Profiling.ProfileName`.
  - URP smoke confirmed `[OB_PERF] summary profile="rendergraph_label_smoke" ...` with no current Unity console errors or warnings.
- Phase 3.5 close-out smoke confirmed:
  - `m_EnableRenderGraph: 1` and `m_EnableRenderCompatibilityMode: 0`.
  - `Open Brush Watermark` and `Open Brush Selection` renderer features remain inactive.
  - HTTP API drew with the brush, saved `C:\Users\andyb\Documents\Open Brush\Snapshots\snapshot_phase35_close.png`, and emitted `[OB_PERF] summary profile="rendergraph_phase35_close" ...`.
  - Current Unity console was clean, and the editor log had no new `Invalid API command`, `IndexOutOfRangeException`, `NullReferenceException`, `HasFreeSpace`, frame-capture, or compile errors after the smoke marker.

Parked exclusions during this pass:

- Selection outline is visually broken and parked.
- `UrpSelectionRendererFeature` still uses compatibility-mode `ScriptableRenderPass.Execute`; keep it disabled until selection work resumes or it gets a real Render Graph port.
- Watermark is parked; `Open Brush Watermark` is disabled even though the feature has a `RecordRenderGraph` implementation.
- ODS/360 capture is parked; it is nonblank, but its current post-processing path is legacy reflection against `OnRenderImage`, which is SRP pass-through under URP.

Deferred validation after Render Graph flip:

- Manual interactive tool use remains pending.
- XR desktop and Android validation remain pending.
- Controlled `[OB_PERF]` comparison against compatibility-mode/pre-URP baselines remains pending. Uncontrolled editor smoke confirms the instrumentation path emits labeled summaries under Render Graph.

## Phase 4: Add `UrpPostProcessingController`

Create:

```text
Assets/Scripts/Rendering/UrpPostProcessingController.cs
```

Status: complete for the current URP non-XR migration gate. XR/mobile runtime validation and visual post-processing match remain deferred to later validation phases.

Current implementation:

- Creates runtime main/capture profile clones.
- Creates a runtime global Volume.
- Ensures `UniversalAdditionalCameraData` on loaded cameras.
- Classifies capture/offscreen cameras conservatively and keeps post-processing off by default.
- Subscribes to `QualityControls.OnQualityLevelChange` and maps bloom off/on from current quality HDR and bloom mode.
- Applies quality HDR and legacy `Fxaa` to main URP cameras through `Camera.allowHDR` and `UniversalAdditionalCameraData.antialiasing`.
- Leaves capture/offscreen cameras without post AA by default; explicit capture opt-in still controls capture post-processing for the render it owns.
- Restricts Phase 2 profile components to known legacy-equivalent Bloom and Vignette only.
- Exposes `DisableLegacyPostProcessing()` as the shared legacy-disable guard; `QualityControls` delegates to it when the controller exists.
- Owns capture post-processing begin/end state for screenshot/API capture callers, including `renderPostProcessing`, volume mask/trigger, and HDR restore.
- `ScreenshotManager` now routes URP capture post-processing setup/restore through the controller when present, with its previous direct setup retained as a fallback.
- HTTP API snapshot/dropcam/video capture paths now use the controller for capture post-processing setup/restore instead of writing `UniversalAdditionalCameraData` fields directly.
- `QualityControls.EnableHDR()` now leaves camera HDR mutation to `UrpPostProcessingController` when the controller exists; the old direct camera writes remain as the Built-in fallback.
- Added narrow HTTP endpoints `quality.get` and `quality.set` so quality/controller application can be smoke-tested without manually driving UI.
- Caches both main and capture URP Bloom/Vignette components from runtime profile clones.
- Exposes `SetCapturePostEffects(bool)` as the controller wrapper around the capture post-processing default.
- Exposes `SetMobileBloomAmount(float)` as the URP-side replacement hook if mobile bloom fade is re-enabled after measurement.

Phase 4 is complete for controller ownership. Legacy post-effect scripts/components are disabled under URP and intentionally left for Phase 11 removal after broader validation.

Responsibilities:

- Register and classify cameras.
- Runtime-instantiate profile assets before modification.
- Cache URP volume components.
- Apply quality bloom, HDR, antialiasing, and capture post-effect state.
- Provide capture overrides.
- Own mobile bloom fade after `MobileBloom` is retired.

Controller integration contract:

- `QualityControls.SetQualityLevel()` should call the controller after it updates quality state, or the controller should subscribe to `OnQualityLevelChange` and pull settings from `QualityControls.m_Instance.AppQualityLevels[index]`.
- Since `OnQualityLevelChange` only passes an index, do not design the controller as if it receives full settings data.
- If mobile bloom fade moves out of `QualityControls.Update()`, the controller needs an explicit tick/update method or `QualityControls` needs to pass the current fade amount each frame.

Suggested APIs:

```csharp
ApplyQuality(int qualityLevel, AppQualitySettingLevels.AppQualitySettings settings)
SetMobileBloomAmount(float amount)
ConfigureScreenshotCamera(Camera camera, bool enableCaptureEffects)
ConfigureDropCamCamera(Camera camera, bool enableBloom, bool enableCaptureEffects)
SetRecordingPostProcessing(Camera camera, bool enabled)
SetCapturePostEffects(bool enabled)
```

Phase 4 progress:

- Added `UrpPostProcessingController.CameraPostProcessingState`.
- Added `BeginCapturePostProcessing()` and `EndCapturePostProcessing()` so capture callers do not duplicate URP camera data mutation and restore logic.
- Routed `ScreenshotManager.RenderToTexture()` capture post-processing through the controller when available.
- Routed HTTP API snapshot/dropcam/video capture post-processing through the controller.
- Phase 4 smoke wrote:
  - `C:\Users\andyb\Documents\Open Brush\Snapshots\snapshot_phase4_controller_2.png`
  - `C:\Users\andyb\Documents\Open Brush\Snapshots\dropcam_phase4_controller_2.png`
  - `C:\Users\andyb\Documents\Open Brush\Videos\video_phase4_controller_2_sequence.txt`
- Smoke emitted `[OB_PERF] summary profile="phase4_capture_controller_smoke_2" ...`.
- Initial compile exposed one stale `CameraPostProcessingState` reference in `ScreenshotManager`; fixed to `UrpPostProcessingController.CameraPostProcessingState` and recompiled cleanly.
- Current Unity console was clean, and the editor log had no new matching API, capture, null-reference, or compile errors after the rerun Phase 4 smoke marker.
- Final Phase 4 smoke after a forced script refresh wrote:
  - `C:\Users\andyb\Documents\Open Brush\Snapshots\snapshot_phase4_final_verified.png`
  - `[OB_PERF] summary profile="phase4_final_controller_smoke_verified" ...`
- The final smoke logged capture post-processing through `[OB_URP_CAPTURE] Enabled URP post-processing for capture camera ScreenshotCamera hdr=True.` and had no new matching API, capture, null-reference, or compile errors after the final marker.
- Phase 4 quality smoke used `quality.set=1` then `quality.set=2`; `UrpPostProcessingController` logged:
  - `[OB_URP_POST] Applied quality=1 bloom=Fast hdr=True fxaa=True.`
  - `[OB_URP_POST] Applied quality=2 bloom=Full hdr=True fxaa=True.`
- Quality smoke emitted `[OB_PERF] summary profile="phase4_quality_controller_smoke" ...`; current Unity console was clean and the editor log had no new matching errors after the smoke marker.
Verification:

- Switching quality levels updates URP profile values through `UrpPostProcessingController.ApplyQuality()`.
- Logs include quality level, bloom mode, HDR, FXAA state, camera classification counts, and capture post-processing activation.
- Final non-XR API smoke passed. XR/mobile runtime validation remains deferred.

## Phase 5: Map Quality Settings to URP

Use existing `AppQualitySettingLevels.BloomMode` as the data model.

Status: complete for quality-to-URP mapping and Windows Editor HTTP smoke. Mobile device runtime validation remains deferred to the mobile validation pass.

| Legacy setting | URP behavior |
| --- | --- |
| `BloomMode.None` | Disable bloom or set intensity to `0`. |
| `BloomMode.Fast` | Enable lower-cost bloom values. |
| `BloomMode.Full` | Enable full-quality bloom values. |
| `BloomMode.Mobile` | Use mobile-tuned values and fade amount. |
| `Hdr = false` | Disable camera HDR and avoid bloom unless LDR bloom is validated. |
| `Hdr = true` | Enable camera HDR where supported. |
| `Fxaa = true` | Use URP camera AA only if supported and visually acceptable in XR. |
| `Fxaa = false` | Disable URP camera AA. |
| `MsaaLevel` | Preserve existing MSAA policy and verify URP pipeline asset interactions. |

Mobile quality:

- Current mobile quality assets serialize `BloomMode.None` for every level, so mobile bloom should remain off unless the quality assets are deliberately changed.
- Preserve dynamic FPS/GPU quality changes.
- Preserve `BloomFadeTime` behavior if mobile bloom is re-enabled.

Verification:

- Desktop quality levels match expected bloom/HDR/AA state.
- Mobile quality changes still occur.
- Mobile bloom remains off with current mobile assets.

Progress:

- `UrpPostProcessingController` maps legacy `Fxaa` to `UniversalAdditionalCameraData.antialiasing = FastApproximateAntialiasing` on main cameras only.
- Capture/offscreen cameras default to `AntialiasingMode.None`; capture post-processing remains opt-in per render path.
- `UrpPostProcessingController` reapplies camera classification after quality changes so added URP camera data, post-processing state, HDR, and AA stay in sync.
- `UrpPostProcessingController` now maps `BloomMode.None`, `Fast`, `Full`, and `Mobile` to distinct URP `Bloom` state:
  - `None`: inactive, intensity `0`.
  - `Fast`: intensity `0.14`, scatter `0.45`, quarter downscale, high-quality filtering off, max iterations `3`.
  - `Full`: intensity `0.2`, scatter `0.55`, half downscale, high-quality filtering on, max iterations `6`.
  - `Mobile`: intensity `0.1 * mobileBloomAmount`, scatter `0.35`, quarter downscale, high-quality filtering off, max iterations `2`.
- `SetMobileBloomAmount(float)` preserves the old mobile bloom fade control point if `BloomMode.Mobile` is deliberately re-enabled later.
- `QualityControls` still owns the existing non-post quality state: shader LOD, MSAA level, anisotropic filtering, simplification, viewport/eye scale, GPU clock level, fixed foveation, and `QualitySettings.SetQualityLevel(...)`.

Phase 5 verification:

- Unity script validation reported no diagnostics for `Assets/Scripts/Rendering/UrpPostProcessingController.cs`.
- HTTP smoke `phase5_quality_mapping_smoke_reloaded` set quality levels `0`, `1`, `2`, and `3`.
- Editor log evidence from that smoke:
  - `quality=0 bloom=None bloomActive=False intensity=0 ... hdr=False fxaa=False msaa=1`.
  - `quality=1 bloom=Fast bloomActive=True intensity=0.14 scatter=0.45 hq=False downscale=Quarter maxIterations=3 hdr=True fxaa=True msaa=1`.
  - `quality=2 bloom=Full bloomActive=True intensity=0.2 scatter=0.55 hq=True downscale=Half maxIterations=6 hdr=True fxaa=True msaa=1`.
  - `quality=3 bloom=Full bloomActive=True intensity=0.2 scatter=0.55 hq=True downscale=Half maxIterations=6 hdr=True fxaa=True msaa=4`.
- `Assets/QualityLevels Mobile.asset` still serializes `m_Bloom: 0` for all seven mobile quality levels, so mobile bloom remains off with current mobile assets.
- Post-marker editor-log error search after `phase5_quality_mapping_smoke_reloaded` found `0` matching API, compile, exception, or null-reference errors.
- Current Unity console error query returned `0` entries after clearing a transient MCP transport disposal error.

## Phase 5.5: Core Feature Cleanup Before Single-Pass

Status: complete for non-deferred items. Items 1 and 6 are explicitly deferred to later phases.

These are remaining non-validation tasks from Phases 1-4 that matter to core app behavior. XR is a core Open Brush target; validation language elsewhere in this plan only describes what has been proven so far, not what counts as core.

1. Deferred to later phase: fix the URP selection outline.
   - Selection feedback is a core interaction feature.
   - Resume from the parked `UrpSelectionRendererFeature` notes in Phase 3.
   - First add a raw-mask debug mode, then distinguish mask generation failure from fullscreen composite failure.
   - Re-enable `Open Brush Selection` only after the URP outline visibly works.

2. Complete Phase 6 capture cleanup.
   - Remove remaining capture-specific legacy assumptions.
   - Ensure snapshots, GIFs, save icons, dropcam, and video use explicit URP/controller-owned post-processing and HDR behavior.
   - Keep save-icon/sketch thumbnail bloom behavior aligned with pre-URP behavior.

3. Harden capture/offscreen camera classification.
   - Replace name-substring heuristics where practical with explicit registration/configuration for app-owned cameras.
   - Cover screenshot, GIF, save icon, dropcam, video, ODS/360, and other capture/offscreen cameras.

4. Decide the production URP video recording path.
   - Either formally make still-frame sequence export the URP production path.
   - Or port the direct ffmpeg/compute-buffer recorder path to a URP-compatible capture point.
   - Avoid leaving production URP behavior dependent on `VideoRecorder.OnRenderImage`.

5. Finish and enable watermark if it remains a supported output feature.
   - `CameraConfig.Watermark` and Lua/API behavior should either work through the URP renderer feature or be explicitly deferred as unsupported for this migration checkpoint.
   - Keep `Open Brush Watermark` disabled until the decision is implemented.

6. Deferred to later phase: replace or explicitly retire ODS/360 legacy post-processing.
   - ODS/360 currently has legacy reflected `OnRenderImage` assumptions.
   - Either add an explicit URP-compatible ODS post/composite path or document that ODS/360 post-processing is intentionally retired/deferred.

7. Clean up `RenderWrapper` ownership enough to prevent hidden legacy coupling.
   - Separate or clearly isolate HDR target policy, MSAA/render-scale policy, recording performance mode, selection mask support, and legacy post-effect toggles.
   - Keep the existing SRP guards, but reduce the chance of future changes reactivating Built-in assumptions under URP.

8. Remove active legacy FXAA/bloom feature tracking from runtime decision paths.
   - Legacy components are disabled, but production decisions should not keep depending on `SENaturalBloomAndDirtyLens` or `FXAA` once URP owns post-processing.
   - Leave file/script deletion for Phase 11.

9. Proceed with single-pass enablement and targeted shader conversion after the blocking items above are either fixed or explicitly deferred.
   - Active runtime/capture shaders should follow the scoped shader audit rather than converting unreferenced remnants.
   - Single-pass is core XR work, not optional polish.

Progress:

- Items 1 and 6 are deferred to later phases at user request.
- Capture cleanup is completed for the currently supported snapshot, GIF, save-icon, dropcam, and still-frame video paths.
- `UrpPostProcessingController` now explicitly registers cameras that pass through controller capture setup, reducing reliance on name-substring capture classification for app-owned capture cameras.
- The production URP video path is the `StillFrameSequenceExporter` path. The direct ffmpeg/compute-buffer recorder path remains Built-in-only unless it is deliberately ported later.
- `Open Brush Watermark` is enabled on `Assets/Settings/Open Brush Universal Render Pipeline Asset_Renderer.asset` so `CameraConfig.Watermark` has a URP renderer-feature implementation.
- `RenderWrapper` no longer carries the unused feature list / `ToggleFeatures` path that could reactivate legacy image-effect components during recording-mode transitions.
- `SaveIconTool.ProgrammaticCaptureSaveIcon()` no longer performs the preliminary direct `Camera.Render()` under SRP; the final save-icon write continues through `ScreenshotManager.RenderToTexture(..., includePostProcessing: true)`.
- Phase 5.5/6 HTTP smoke outputs:
  - `C:\Users\andyb\Documents\Open Brush\Snapshots\phase55_phase6_snapshot_fixed.png`
  - `C:\Users\andyb\Documents\Open Brush\Snapshots\phase55_phase6_dropcam_fixed.png`
  - `C:\Users\andyb\Documents\Open Brush\Snapshots\phase55_phase6_saveicon_fixed.png`
  - `C:\Users\andyb\Documents\Open Brush\Videos\phase55_phase6_video_fixed_sequence.txt`
  - `C:\Users\andyb\Documents\Open Brush\Snapshots\phase55_phase6_autogif.gif`
  - `C:\Users\andyb\Documents\Open Brush\Snapshots\phase55_phase6_timegif.gif`
- Current Unity console error query returned `0` entries after the final smoke.
- Post-marker editor-log search after `phase55_phase6_timegif_smoke` found `0` real API, compile, exception, null-reference, Render Graph render-pass, or save-icon render errors.

## Phase 6: Replace Capture-Specific Legacy Component Logic

Do this before legacy component cleanup.

Status: complete for current URP capture cleanup. TiltShift remains deferred unless a confirmed visible dependency appears.

1. Replace hard dependencies/assertions in:
   - `ScreenshotManager`
   - `DropCamPreviewScreen`
2. Remove assumptions that `MobileBloom` or `SENaturalBloomAndDirtyLens` must exist on capture objects.
3. Preserve mobile screenshot/dropcam HDR-off behavior unless testing proves HDR is safe.
4. Keep `CameraConfig.PostEffects`, `Camera_PostEffects`, `ToggleCameraPostEffects`, and Lua `App:PostProcessing(...)` behavior.
5. Map:
   - `Kino.Vignette` -> URP `Vignette`.
   - `TiltShift` -> defer unless there is a confirmed visible dependency.

Verification:

- Screenshot and dropcam paths do not assert about missing legacy bloom components.
- Capture post-effects toggle still persists and affects only intended capture stylization.

Progress:

- `ScreenshotManager` no longer asserts about missing `MobileBloom` or `SENaturalBloomAndDirtyLens` while an SRP is active; the legacy assertions remain for Built-in fallback.
- `DropCamPreviewScreen` no longer asserts about missing `MobileBloom` or `SENaturalBloomAndDirtyLens` while an SRP is active; the legacy assertions remain for Built-in fallback.
- Mobile screenshot/dropcam HDR-off behavior is preserved.
- HTTP `capture.dropcam` now restores the dropcam active state in `finally` if the render path throws.
- Snapshot, Auto GIF, Time GIF, save-icon/sketch-thumbnail, dropcam, and still-frame video capture paths now use explicit URP capture post-processing/HDR setup through `UrpPostProcessingController` or `ScreenshotManager.RenderToTexture()`.
- Save-icon/sketch-thumbnail capture remains always post-processed to match the pre-URP always-bloom behavior.
- The stale preliminary `SaveIconCamera.Render()` call is skipped under SRP to avoid Render Graph render-pass errors; Built-in fallback behavior is preserved.
- `Kino.Vignette` is not re-enabled as a legacy component under URP. The controller owns URP `Vignette` components on runtime profiles and keeps capture vignette inactive unless a later visible dependency requires it.
- `CameraConfig.PostEffects`, `Camera_PostEffects`, `ToggleCameraPostEffects`, and Lua `App:PostProcessing(...)` remain the capture post-processing control path.

## Phase 7: Enable Single-Pass Instanced Per Target Platform

Enable after URP camera/post baseline and critical render hook replacements are testable.

1. Set OpenXR render mode to single-pass instanced for target platforms:
   - Standalone.
   - Android.
   - Metro only if required.
2. Commit the settings change separately.
3. Run with headset or XR Simulation.
4. Record failures by category:
   - wrong eye
   - doubled geometry
   - missing geometry
   - black/fullscreen output
   - capture/offscreen regressions

Expected result:

- Shader Graph brush strokes should mostly render correctly.
- Hand-written runtime shaders and fullscreen passes are the likely failure points.

## Phase 8: Targeted Single-Pass Shader Conversion

Use `XR-URP-SHADER-AUDIT.md` as the conversion scope.

Current first-pass scope:

- Convert or retire by earlier phases: 114 GUID-referenced project shaders.
- Convert or retire by earlier phases: `Assets/Shaders/BlitDownsample.shader`, which is active by `Shader.Find("Hidden/BlitDownsample")`.
- Skip for first pass: the 14 deferred static-unreferenced project shaders in the audit.
- Validate before editing: package Shader Graph brush shaders, because Shader Graph should usually handle single-pass instancing.
- Validate or cheap-convert if needed: package environment shaders in the unknown/dynamic bucket.

For relevant hand-written vertex/fragment shaders:

1. Add `#pragma multi_compile_instancing` to each relevant `CGPROGRAM` or `HLSLPROGRAM` block.
2. Add instance transfer in vertex functions when the input/output structs support it:

```hlsl
UNITY_TRANSFER_INSTANCE_ID(input, output);
```

Use the actual variable names, not always `v` and `o`.

3. For fragment functions that read stereo-dependent data, ensure:

```hlsl
UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
```

4. For geometry shader passes, explicitly transfer stereo/instance data across vertex -> geometry -> fragment stages.
5. Compile in small batches:
   - UI/panels.
   - controllers/pointers.
   - environment/skybox.
   - selection/stencil.
   - fullscreen/capture.
   - package runtime shaders.

Do not convert unreferenced/remnant shaders in the first pass.

Verification:

- Each batch compiles before proceeding.
- Runtime category checks pass in both eyes.

## Phase 9: Local Package Runtime Shader Fixes

Because the project now points to the local package, package shader fixes should be made in:

```text
../open-brush-unity-tools/Packages/open-brush-unity-tools
```

Known candidates:

```text
Runtime/Resources/Environments/Shaders/LinearGradient.shader
Runtime/Resources/Environments/Shaders/ParticleDustBokeh.shader
```

Also verify, but do not preemptively change unless testing shows breakage:

```text
Runtime/Shaders/2_UnlitSpecials/Toon/ToonOutline.shader
Runtime/Shaders/2_UnlitSpecials/Toon/ToonMultiMaterial.shader
```

Verification:

- Environment gradients and dust render in both eyes.
- Toon brushes still render correctly.

## Phase 10: Unified Fullscreen, Blit, Capture, and XR Texture Review

Review only active shaders/passes from the shader audit.

Known candidates:

- `BlitDownsample`
- `BlitLinearToGamma`
- `BlitLdrPmaOverlay`
- `BlitWatermark`
- `BlitToCompute`
- `FixDistortion`
- `FixDistortionAndReveal`
- selection outline shaders
- watermark path
- video capture blit path
- URP bloom/final post stack

Rules:

- Apply XR screen-space transforms only when sampling XR camera textures.
- Do not apply XR transforms to mono screenshot/dropcam/video/offscreen textures unless the source is actually an XR texture array/slice.
- Prefer URP texture declarations and render-pass APIs over legacy `tex2D` assumptions when changing active URP paths.

For manual screen-space UV sampling in XR paths:

```hlsl
UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
uv = UnityStereoTransformScreenSpaceTex(uv);
```

Verification:

- Bloom, fade, watermark, distortion/reveal, selection, and overlays are aligned in both eyes.
- Mono captures are not cropped, doubled, or eye-offset.

## Phase 11: Retire Legacy Post Components

Status: partially advanced during Phase 2 to avoid double post-processing while matching the pre-URP look. Do not delete scripts/shaders until URP replacements and capture paths pass validation.

Completed during Phase 2:

- [x] Disabled production scene/prefab instances of `SENaturalBloomAndDirtyLens`, `FXAA`, `MobileBloom`, `TiltShift`, `Kino.Vignette`, and `PostEffectsToggle` where present in `Main.unity`, XR rig, ODS, and LIV camera assets.
- [x] Removed legacy bloom/FXAA feature registration from `RenderWrapper`.
- [x] Removed legacy post component lists from `QualityControls`.
- [x] Forced `PostEffectsToggle` to keep legacy vignette/tilt-shift disabled.

Remaining, only after URP replacements pass validation:

1. Remove/disable production dependencies on:
   - `SENaturalBloomAndDirtyLens`
   - `FXAA`
   - `MobileBloom`
   - `TiltShift`
   - `Kino.Vignette`
2. Replace capture-specific legacy assumptions in screenshot/dropcam/video code.
3. Keep old shaders/files only if still needed for fallback or historical compatibility; otherwise mark them as remnants in the shader audit.

Verification:

- Project search confirms production cameras no longer depend on legacy post components.
- Builds have no missing-script or missing-shader warnings.

## Phase 12: Mobile Bloom Fallback Only If Measured

If built-in URP bloom is too expensive or visually unsuitable on target hardware, create a custom URP `ScriptableRendererFeature` based on the useful parts of `MobileBloom`:

- central crop bloom
- `BloomLevels` downsample chain
- optional eye-alternating saved bloom reuse
- sky-brightness bloom reduction
- pass scheduled after transparents and before final post

Do not start here. Measure first.

## Final Acceptance Tests

### Editor Desktop

- Main scene renders without legacy post effects.
- Quality levels update bloom/HDR/AA.
- Selection outline works.
- Watermark toggle works.
- Video recording captures frames.
- Screenshot/dropcam paths work.

### Standalone OpenXR

- Single-pass instanced renders both eyes correctly.
- UI, controllers, panels, teleporter, environment, and overlays are correct.
- Fullscreen effects have no eye mismatch.

### Android OpenXR

- Single-pass instanced renders both eyes correctly.
- URP post stack has acceptable frame cost.
- Dynamic mobile quality still changes level.
- Mobile bloom remains off while quality data says `BloomMode.None`.

### Capture and API

- Screenshot with capture post effects on/off.
- Dropcam preview.
- Video recording with post-processing disabled when required.
- Lua `App:PostProcessing(...)` controls intended capture effects.
- Lua/API watermark toggle works.

### Shader Scope

- Referenced runtime shaders are converted or explicitly deferred.
- Unreferenced/remnant shaders are documented and left untouched.
- Unknown dynamic shaders have a validation note or fallback decision.

## Risks and Open Questions

- URP 17.3 field names and serialized data may differ from what older URP migration notes assume.
- Selection and video recording may require custom URP passes before post-processing can be considered migrated.
- Camera antialiasing behavior in XR must be validated on device; MSAA may remain preferable to post AA.
- Dynamic `Shader.Find` paths can hide references from static GUID searches.
- Some unreferenced shaders may still be loaded by external sketches, plugins, or imported content; classify these as unknown rather than deleting them.
