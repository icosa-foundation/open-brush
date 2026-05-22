# XR Single-Pass Instanced — Migration Plan

## Background

Open Brush currently uses OpenXR in **multi-pass** mode (`m_renderMode: 0`), rendering the scene twice — once per eye. Switching to **single-pass instanced** renders both eyes in one GPU draw call per object, roughly halving CPU draw call overhead.

### What's already handled

- **Brush shaders**: All in `open-brush-unity-tools` as URP Shader Graphs. Shader Graph automatically emits correct XR single-pass instanced code — no changes needed there.
- **Toon brush shaders** (`ToonMultiMaterial.shader`, `ToonOutline.shader`): Already have correct URP HLSL subshaders with all required stereo macros (`UNITY_TRANSFER_INSTANCE_ID`, `UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX`, etc.).
- **Intersection system** (`GpuIntersector.cs`): Renders via a dedicated `Camera.RenderWithShader()` to a private `RenderTexture` — not part of the XR eye buffer path at all. Its geometry shader is not a blocker.

### What needs work

The remaining scope is two groups of hand-written CGPROGRAM shaders:

1. **`Assets/Shaders/` (~115 shaders)** — UI, panels, controller effects, post-processing, environment. All are URP-tagged. Most already have partial stereo infrastructure in their vertex structs (`UNITY_VERTEX_OUTPUT_STEREO`, `UNITY_SETUP_INSTANCE_ID`, `UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO`), but are uniformly missing `#pragma multi_compile_instancing` and `UNITY_TRANSFER_INSTANCE_ID`.

2. **`open-brush-unity-tools` package — environment shaders** (`LinearGradient.shader`, `ParticleDustBokeh.shader`): Same pattern as above. The local install at `../open-brush-unity-tools/` can be edited directly now that the manifest points to it via `file:` path.

The **blit/fullscreen shaders** (`FixDistortion`, `BlitDownsample`, `BlitLinearToGamma`, `BlitLdrPmaOverlay`, `BlitWatermark`, `MobileBloom`) are a separate concern: they sample render textures and may need stereo-aware UV handling depending on how URP's XR system routes their render passes.

---

## Phase 1 — Enable single-pass instanced and verify baseline

**Goal**: Turn on single-pass instanced in the project settings and see what breaks immediately, before touching any shaders.

### Steps

1. In Unity: **Edit → Project Settings → XR Plug-in Management → OpenXR**, set **Render Mode** to **Single Pass Instanced**.  
   This changes `m_renderMode` from `0` to `1` in `Assets/XR/Settings/OpenXRPackageSettings.asset`.

2. Build or play in the editor with the headset connected (or via XR Simulation).

3. Note which objects render incorrectly — typically: wrong eye, doubled geometry, or black/missing content. These are the shaders that need fixing most urgently.

4. Commit the settings change on its own so it's easy to revert if needed.

**Expected outcome at this point**: Brush strokes (Shader Graph) will likely render correctly. Most UI panels and controller geometry will probably be wrong.

---

## Phase 2 — Batch-fix `Assets/Shaders/` CGPROGRAM shaders

**Goal**: Add the two missing lines to all vertex/fragment shaders in `Assets/Shaders/`. This is mechanical and scriptable.

### What to add to each shader

Every CGPROGRAM block needs:

```hlsl
// After the existing #pragma lines:
#pragma multi_compile_instancing

// In the vertex function, immediately after UNITY_SETUP_INSTANCE_ID(v):
UNITY_TRANSFER_INSTANCE_ID(v, o);
```

The `UNITY_TRANSFER_INSTANCE_ID` line must appear in the vertex function body — it copies the eye index from input to output struct so the fragment shader can decode it. Without it, `UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX` in the fragment stage has nothing to read.

### Approach

Write a PowerShell script to process all `.shader` files under `Assets/Shaders/`. The patterns are consistent enough to automate:

- Insert `#pragma multi_compile_instancing` after the last `#pragma` block in each CGPROGRAM.
- Insert `UNITY_TRANSFER_INSTANCE_ID(v, o);` on the line after any `UNITY_SETUP_INSTANCE_ID(` call inside a vertex function.

Run the script, compile in Unity, fix any per-file issues that the automation gets wrong. This will cover ~110 of the ~115 shaders.

### Shaders to handle manually

A few shaders have non-standard vertex function shapes or multiple passes that need individual attention:

- `FlatLit.shader` — has a geometry shader pass. The geometry stage needs `UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX` and `UNITY_TRANSFER_INSTANCE_ID` between the vertex and geometry stages. Check whether it is actually in the main render path (it may not be).
- `MobileBloom.shader` — multi-pass with a GrabPass-style texture sample. Needs stereo texture sampling review (see Phase 3).
- `ColorPicker_*.shader` (5 variants) — fullscreen HSL/HSV pickers. These are rendered to a texture off the main camera path; confirm whether they run in XR context at all.
- `GrabHighlightMask.shader` / `GrabHighlightUnmask.shader` — stencil-based outline system. Confirm the render pass is invoked per-eye by URP.

### Surface shaders

A small number of shaders in this folder use `#pragma surface` (e.g. `StandardAudioReactive.shader`, `StandardBlendToFog.shader`). Surface shaders handle instancing differently — Unity's surface shader compiler generates the instancing boilerplate. For these, add `#pragma multi_compile_instancing` and verify compilation; do not manually add `UNITY_TRANSFER_INSTANCE_ID`.

---

## Phase 3 — Blit and fullscreen effect shaders

**Goal**: Ensure screen-space effects sample the correct eye's render texture.

In URP + XR, `ScriptableRenderPass` callbacks are invoked once per eye. If the blit shaders sample `_CameraColorTexture` or similar via standard `tex2D`, they may work correctly without modification. If they sample via hardcoded UV coordinates or compute screen-space UVs manually, they need the stereo transform applied.

### Shaders to review

| Shader | Issue to check |
|--------|---------------|
| `BlitDownsample.shader` | UVs derived from `_MainTex_TexelSize` — should be fine |
| `BlitLinearToGamma.shader` | Simple blit — should be fine |
| `BlitLdrPmaOverlay.shader` | Premultiplied alpha blit — should be fine |
| `BlitWatermark.shader` | Check UV computation |
| `BlitToCompute.shader` | Writes to a compute buffer — check if invoked per-eye |
| `FixDistortion.shader` | Samples three textures with custom ray math — likely needs per-eye UV correction |
| `FixDistortionAndReveal.shader` | Same as above |
| `MobileBloom.shader` | Multi-pass bloom — review each pass |

For any shader that computes screen-space UVs manually, wrap the UV calculation with:

```hlsl
UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
// then use UnityStereoTransformScreenSpaceTex(uv) on any screen-space sample
```

---

## Phase 4 — `open-brush-unity-tools` environment shaders

**Goal**: Fix the two non-Shader-Graph shaders in the package.

Since the manifest now points to the local install at `../open-brush-unity-tools/`, changes here can be made directly and will be reflected in the project immediately.

### Files

```
../open-brush-unity-tools/Packages/open-brush-unity-tools/Runtime/Resources/Environments/Shaders/LinearGradient.shader
../open-brush-unity-tools/Packages/open-brush-unity-tools/Runtime/Resources/Environments/Shaders/ParticleDustBokeh.shader
```

Both are CGPROGRAM, both already have the vertex struct stereo setup. Apply the same two-line fix as Phase 2.

Note: `ToonMultiMaterial.shader` and `ToonOutline.shader` already have correct URP HLSL subshaders. They do not have `#pragma multi_compile_instancing` in the HLSLPROGRAM block, but URP's XR integration may inject stereo keywords automatically for HLSL passes that include `Core.hlsl`. Verify in practice; add the pragma if stereo rendering is broken.

---

## Phase 5 — Test and validate

### Per-category checklist

- [ ] **Brush strokes** render correctly in both eyes (all brush types — particle, tube, diffuse, standard, Toon)
- [ ] **UI panels** (tool panels, color picker, gallery) appear correctly positioned in both eyes
- [ ] **Controller models** and pointer ray render correctly
- [ ] **Environment / skybox** renders correctly (LinearGradient, Skybox.shader)
- [ ] **Selection outline** (GrabHighlightMask / stencil system) renders correctly
- [ ] **Fade to black** and scene transitions work
- [ ] **FixDistortion** (used in specific headset modes) shows no eye-swap artifacts
- [ ] **Bloom / post-processing** does not show doubled or misaligned effects
- [ ] **Teleporter** and ground plane overlays appear in both eyes
- [ ] **ODS rendering** still works (separate render path; single-pass change should not affect it, but verify)

### Performance

After confirming correctness, capture a GPU frame in RenderDoc or the Unity Profiler and verify draw calls are instanced (look for `DrawMeshInstancedIndirect` or GPU instancing markers). The CPU draw call count to the headset should roughly halve compared to multi-pass.

---

## Notes

- The `Intersection.shader` geometry shader is **not a concern** — `GpuIntersector` renders via a private `Camera` to a `RenderTexture` that is never presented to the headset. It runs outside the XR stereo path entirely.
- The legacy shaders in `Assets/Resources/Brushes/` are **not used** at runtime — brushes are rendered via the Shader Graphs in the `open-brush-unity-tools` package. They can be ignored.
- The ODS render path (`ODS_RENDER` / `ODS_RENDER_CM` multi_compile keywords present in many brush shaders) is a separate 360° video capture mode and is unaffected by the XR render mode change.
