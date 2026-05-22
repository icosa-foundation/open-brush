# XR/URP Shader Usage Audit

Status: Phase 1 classification complete for planning. Unity import/compile and headset validation are still pending.

## Scope

This audit supports Phase 1 of `COMBINED-XR-URP-MIGRATION-PLAN.md`.

Inputs checked:

- `Assets/Shaders/**/*.shader`
- `../open-brush-unity-tools/Packages/open-brush-unity-tools/**/*.shader`
- `../open-brush-unity-tools/Packages/open-brush-unity-tools/**/*.shadergraph`
- Static GUID references in `.mat`, `.prefab`, `.unity`, `.asset`, `.controller`, `.playable`, `.renderTexture`, `.shadergraph`, and `.cs`
- Runtime inclusion paths through build scenes, manifests, `Resources.Load`, `Shader.Find`, serialized `Shader` fields, `Graphics.Blit`, command buffers, and `RenderWithShader`

Important limitation:

- A GUID reference proves an asset references a shader, but not that the asset is exercised by a normal runtime path.
- `Shader.Find`, package Resources, imported sketches, plugins, and generated materials can hide dynamic references. Anything plausible but not proven is classified conservatively.

## Runtime Inclusion Roots

Enabled build scenes:

```text
Assets/Scenes/Loading.unity
Assets/Scenes/Main.unity
```

Runtime content roots:

- `App.ManifestFull` merges the standard and experimental manifests, or uses the Zapbox manifest for Zapbox builds.
- `BrushCatalog.LoadBrushesInManifest()` loads brushes from `App.Instance.ManifestFull`.
- `EnvironmentCatalog.LoadEnvironmentsInManifest()` loads environments from `App.Instance.ManifestFull`.
- `SceneSettings` loads environment prefabs with `Resources.Load<GameObject>(env.m_EnvironmentPrefab)`.
- `SceneSettings` loads custom skybox materials with `Resources.Load<Material>("Environments/CustomSkybox")` and `Resources.Load<Material>("Environments/CustomStereoSkybox")`.
- `ShaderWarmup` warms shaders from `BrushCatalog.m_Instance.AllBrushes.Select(x => x.Material)`.

This means manifest-referenced brushes/environments and their Resources dependencies are runtime scope even when they are not directly referenced by `Main.unity`.

## Summary

| Area | Files | Referenced by GUID | Not referenced by GUID |
| --- | ---: | ---: | ---: |
| `Assets/Shaders` `.shader` files | 129 | 114 | 15 |
| local package `.shader`/`.shadergraph` files | 54 | 52 | 2 |

Additional findings:

- `Assets/Shaders/BlitDownsample.shader` is not referenced by GUID, but is code-created by `RenderWrapper` via `Shader.Find("Hidden/BlitDownsample")`; it is active.
- `Assets/Shaders/BlitLdrPmaOverlay.shader` is serialized on `WatermarkEffect` instances and is the active watermark shader path.
- `Assets/Shaders/BlitWatermark.shader` appears unused in this branch.
- `Assets/Materials/ReferenceImageIcon.mat` uses `GalleryIcon.shader`, not `ReferenceImageIcon.shader`.
- Package brush shadergraphs are mostly referenced through package brush/material assets and are runtime scope.
- Package environment shaders `LinearGradient.shader` and `ParticleDustBokeh.shader` are not referenced by GUID in the current scan. Keep them unknown/dynamic because package environment resources can still load by name.

## Conversion Scope Decision

For the first single-pass pass, convert only:

- GUID-referenced project shaders that are included through scenes, Materials, Prefabs, Resources, manifests, or runtime scripts.
- Active render/capture/fullscreen shaders, including code-only shaders.
- Package shadergraphs/shaders that are in manifest-driven brush/environment runtime paths, if testing shows a stereo issue.
- Unknown/dynamic shaders only when they are cheap to convert or runtime validation proves they are loaded.

Do not spend first-pass time on static-unreferenced project shaders unless a later runtime validation finds them.

## High-Priority Runtime Buckets

### Main Runtime UI, Controllers, Environment, and App Surfaces

These GUID-referenced project shaders are in runtime scene/material/prefab/resource paths and should remain in the first conversion candidate set:

```text
Assets/Shaders/360PanoramaWarp.shader
Assets/Shaders/AlphaCutoutPulse.shader
Assets/Shaders/AlphaOutline.shader
Assets/Shaders/AmbientGrid.shader
Assets/Shaders/AmbientLit.shader
Assets/Shaders/AngleIndicator.shader
Assets/Shaders/AnimalRuler.shader
Assets/Shaders/AssetLoading.shader
Assets/Shaders/AudioReactiveBrushIcon.shader
Assets/Shaders/AudioReactiveIcon.shader
Assets/Shaders/BorderSphere.shader
Assets/Shaders/BrowserButton.shader
Assets/Shaders/BrushSizePad.shader
Assets/Shaders/ChangeSliderValue.shader
Assets/Shaders/CircularProgress.shader
Assets/Shaders/ColorPicker.shader
Assets/Shaders/ColorPicker_hl_s.shader
Assets/Shaders/ColorPicker_hs_l.shader
Assets/Shaders/ColorPicker_hs_logv.shader
Assets/Shaders/ColorPicker_sl_h.shader
Assets/Shaders/ColorPicker_sv_h.shader
Assets/Shaders/ControllerActivationEffect.shader
Assets/Shaders/ControllerResetPad.shader
Assets/Shaders/ControllerSwapEffect.shader
Assets/Shaders/ControllerXRay.shader
Assets/Shaders/CustomMirrorGuide.shader
Assets/Shaders/DefaultMaterialPreview.shader
Assets/Shaders/EmissivePulse.shader
Assets/Shaders/FlatLit.shader
Assets/Shaders/FlipbookwithAlpha.shader
Assets/Shaders/FogDensity.shader
Assets/Shaders/FullScreenOverlay.shader
Assets/Shaders/GalleryIcon.shader
Assets/Shaders/GpuText.shader
Assets/Shaders/GripPulse.shader
Assets/Shaders/GroundPlaneOverlay.shader
Assets/Shaders/HighlightPulse.shader
Assets/Shaders/LaserPointerLine.shader
Assets/Shaders/LightWidget.shader
Assets/Shaders/LinearGradient.shader
Assets/Shaders/LinearGradientPreview.shader
Assets/Shaders/MobileDiffuse.shader
Assets/Shaders/ModelGhost.shader
Assets/Shaders/MoreMenuBG.shader
Assets/Shaders/NewSketchButton.shader
Assets/Shaders/OutlineMesh.shader
Assets/Shaders/PadTimer.shader
Assets/Shaders/Panel.shader
Assets/Shaders/PanelButton.shader
Assets/Shaders/PanelButtonCutout.shader
Assets/Shaders/PanelButton_Atlas.shader
Assets/Shaders/PanelButton_AtlasActive.shader
Assets/Shaders/ParticleDustBokeh.shader
Assets/Shaders/Passthrough.shader
Assets/Shaders/Pcx/Point.shader
Assets/Shaders/PinCushionItem.shader
Assets/Shaders/Pointer.shader
Assets/Shaders/PointerPulse.shader
Assets/Shaders/PointerScreenSpace.shader
Assets/Shaders/PopupBorder.shader
Assets/Shaders/ProfileIcon.shader
Assets/Shaders/ProfileIconGUI.shader
Assets/Shaders/ProgressBar.shader
Assets/Shaders/ProgressRing.shader
Assets/Shaders/ReferenceImage.shader
Assets/Shaders/ScrollingIcons.shader
Assets/Shaders/SelectionToggle.shader
Assets/Shaders/SharingState.shader
Assets/Shaders/ShowcaseTab.shader
Assets/Shaders/SketchSurface.shader
Assets/Shaders/SketchbookButton.shader
Assets/Shaders/Skybox.shader
Assets/Shaders/SnapGrid3D.shader
Assets/Shaders/SnapshotCameraFlash.shader
Assets/Shaders/StandardAudioReactive.shader
Assets/Shaders/StandardNoFog.shader
Assets/Shaders/StandardforIcons.shader
Assets/Shaders/StandardwithOutlineFlatten.shader
Assets/Shaders/StencilSurface.shader
Assets/Shaders/StencilSurfaceTutorial.shader
Assets/Shaders/SwatchAdditive.shader
Assets/Shaders/SwatchBloom.shader
Assets/Shaders/SwipeHint.shader
Assets/Shaders/TeleporterFeet.shader
Assets/Shaders/TeleporterLine.shader
Assets/Shaders/TextureNoFog.shader
Assets/Shaders/TextureNoFogCutout.shader
Assets/Shaders/TextureNoFogCutoutTint.shader
Assets/Shaders/TextureNoFogNoCull.shader
Assets/Shaders/TextureNoFogNoCullCutout.shader
Assets/Shaders/TextureNoFogTransparent.shader
Assets/Shaders/ThreeDeeFadeText.shader
Assets/Shaders/ThreeDeeText.shader
Assets/Shaders/TiltBrushLogo_Progress.shader
Assets/Shaders/TransformControllerOverlay.shader
Assets/Shaders/TransformLine.shader
Assets/Shaders/TransformLineHint.shader
Assets/Shaders/TutorialNotification.shader
Assets/Shaders/Unlit-BackFaces.shader
Assets/Shaders/Unlit-ScrollingCutout.shader
Assets/Shaders/Unlit-TextureCutoutNoCull.shader
Assets/Shaders/Unlit-TextureNoCull.shader
Assets/Shaders/Unlit-TextureTint.shader
Assets/Shaders/UnlitHDRColorButton.shader
Assets/Shaders/UnlitOutlineFlatten.shader
Assets/Shaders/WaveformIndicator.shader
```

### Render, Capture, Fullscreen, and Legacy Hook Shaders

These require special handling because correctness depends on whether the source is an XR eye texture, a mono render texture, or a capture buffer:

```text
Assets/Shaders/BlitDownsample.shader
Assets/Shaders/BlitLdrPmaOverlay.shader
Assets/Shaders/BlitToCompute.shader
Assets/Shaders/FixDistortion.shader
Assets/Shaders/FixDistortionAndReveal.shader
Assets/Shaders/GrabHighlightMask.shader
Assets/Shaders/GrabHighlightUnmask.shader
Assets/Shaders/MobileBloom.shader
Assets/Shaders/PolyAssetThumbnail.shader
Assets/Shaders/SnapshotCameraFlash.shader
```

Notes:

- `BlitDownsample.shader` is code-only active through `RenderWrapper`.
- `BlitLdrPmaOverlay.shader` is the active watermark path.
- `BlitToCompute.shader` is serialized through config/video capture and should be reviewed with `VideoRecorder`.
- `MobileBloom.shader` is still serialized in `Main.unity`, but the plan should retire it in favor of URP bloom unless profiling says otherwise.

### Package Runtime Brush and Environment Shaders

Package brush shadergraphs are runtime scope because brush descriptors/materials are manifest driven. Shader Graph should usually handle single-pass instancing, so validate representative brush families before editing package shadergraphs.

Referenced hand-written package shaders:

```text
../open-brush-unity-tools/Packages/open-brush-unity-tools/Runtime/Shaders/2_UnlitSpecials/Toon/ToonMultiMaterial.shader
../open-brush-unity-tools/Packages/open-brush-unity-tools/Runtime/Shaders/2_UnlitSpecials/Toon/ToonOutline.shader
```

Unknown/dynamic package environment shaders:

```text
../open-brush-unity-tools/Packages/open-brush-unity-tools/Runtime/Resources/Environments/Shaders/LinearGradient.shader
../open-brush-unity-tools/Packages/open-brush-unity-tools/Runtime/Resources/Environments/Shaders/ParticleDustBokeh.shader
```

## Deferred Project Shaders

These project shaders had no static GUID reference outside their own metadata and no direct current code/string hit found in the project scan. Do not convert in the first single-pass pass unless runtime validation finds a live reference.

```text
Assets/Shaders/Blended.shader
Assets/Shaders/BlitLinearToGamma.shader
Assets/Shaders/BlitWatermark.shader
Assets/Shaders/FadeToBlack.shader
Assets/Shaders/Pcx/Disk.shader
Assets/Shaders/ReferenceImageIcon.shader
Assets/Shaders/StandardBlendToFog.shader
Assets/Shaders/StandardWithOutline.shader
Assets/Shaders/ToolPanel.shader
Assets/Shaders/TwoTexAnimation.shader
Assets/Shaders/Unlit-Diffuse.shader
Assets/Shaders/Unlit-DiffuseNoCull.shader
Assets/Shaders/VisualizerRing.shader
Assets/Shaders/VisualizerStage.shader
```

Specific notes:

- `ReferenceImageIcon.shader`: `Assets/Materials/ReferenceImageIcon.mat` currently references `GalleryIcon.shader`.
- `BlitWatermark.shader`: current `WatermarkEffect` instances use `BlitLdrPmaOverlay.shader`.
- `FadeToBlack.shader`: fade behavior exists in code, but this shader did not show a current static or string reference.
- `VisualizerRing.shader` and `VisualizerStage.shader`: visualizer code exists, but these shader files did not show current static references. Verify visualizer prefabs/materials before deleting or ignoring permanently.

## Phase 1 Outcome

The conversion candidate set is now reduced from "all shaders" to:

- 114 GUID-referenced project shaders.
- 1 additional active code-only project shader: `BlitDownsample.shader`.
- 52 package shader/shadergraph files already referenced by package assets, with Shader Graph edits deferred until validation proves they are needed.
- 2 package environment shaders in unknown/dynamic scope.

The first conversion pass should explicitly skip the 14 deferred project shaders above.

## Validation Still Needed

- Let Unity reload the local package and confirm there are no missing package/shader references.
- Search the full Unity Editor log for `[OB_SHADER_AUDIT]` once runtime logging is added.
- Run Main scene with representative environments and brushes.
- Validate capture, video, watermark, selection, screenshot, and dropcam paths after render-hook migration.
- Revisit deferred shaders only if Unity logs, missing-shader warnings, imported sketches, or runtime testing point to them.
