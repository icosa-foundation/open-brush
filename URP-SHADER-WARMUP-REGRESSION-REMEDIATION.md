# URP Shader Warmup Regression Remediation Plan

## Scope

This plan covers shader warmup issues that became newly significant with the URP migration. The existing `ShaderWarmup` scene/prefab path still exists, but URP and ShaderGraph introduce more generated passes, more pipeline keywords, and more stripping risk than the pre-URP custom shader setup.

## Evidence

- `ProjectSettings/GraphicsSettings.asset` uses a URP render pipeline asset via `m_CustomRenderPipeline`.
- `ProjectSettings/GraphicsSettings.asset` preloads `Assets/Resources/UnityGLTF Shader Variants.shadervariants`, but there is no equivalent Open Brush brush/URP shader variant collection.
- `Assets/Scripts/Rendering/ShaderWarmup.cs` warms by creating visible quads with distinct materials, plus `SELECTION_ON` copies.
- That runtime quad approach does not explicitly cover all URP generated passes, pipeline asset variants, renderer-feature variants, or stripped ShaderGraph variants.

## Risks Introduced Or Worsened By URP

1. URP/ShaderGraph pass coverage is incomplete.

   Rendering a single quad usually exercises the visible forward pass. It does not prove that all generated passes and variants needed later are present or warmed.

2. Pipeline keyword coverage is incomplete.

   URP adds pipeline-level variants for lighting, shadows, fog, depth/normal passes, XR, renderer features, and similar settings. The existing warmup enumerates material keywords, not pipeline variants.

3. Build stripping can remove required brush variants.

   ShaderGraph and URP stripping may remove variants not represented in scenes, material references, or variant collections. Runtime material creation during warmup cannot resurrect stripped variants.

4. Startup warmup may give false confidence.

   If variants were already stripped or only non-forward passes are missing, the current warmup can finish without proving that later brush rendering paths are safe.

## Remediation Plan

### 1. Inventory Required Brush Variants

Create an editor-only scanner that enumerates:

- all brush catalog materials,
- replacement package materials used by imported Open Brush strokes,
- runtime keyword states currently toggled by Open Brush code,
- important material keyword/property combinations such as `SELECTION_ON`, `AUDIO_REACTIVE`, baked export variants, scripting/clipping, and vertex-position mode variants.

Output a deterministic report under a generated folder, for example:

`Assets/Generated/ShaderWarmup/open-brush-brush-variant-inventory.json`

The report should include:

- material asset path,
- shader asset path,
- enabled keywords,
- local shader keywords,
- render queue/blend/cull state where relevant,
- source brush GUID/name.

### 2. Generate A Brush ShaderVariantCollection

Build an editor script that creates or updates:

`Assets/Generated/ShaderWarmup/OpenBrushBrushVariants.shadervariants`

The collection should be generated from the inventory above. It should include URP brush shaders and their material keyword combinations. It should be deterministic so diffs are reviewable.

Add this collection to `GraphicsSettings.asset` `m_PreloadedShaders`.

### 3. Add URP Build-Time Variant Audit

Add an editor validation step that runs before builds and fails or warns when:

- a brush material uses a shader not represented in the generated collection,
- a known runtime keyword combination is missing,
- a material is mapped to a package shadergraph but absent from the warmup inventory,
- a brush shader has no generated variant coverage.

This can start as a menu command and later be integrated into CI/build scripts.

### 4. Preserve Runtime Warmup, But Narrow Its Role

Keep `ShaderWarmup.cs` for runtime GPU-driver warmup, but treat it as a second layer after build-time variant preservation.

Update comments/code naming to clarify:

- the ShaderVariantCollection protects variants from stripping and preloads them,
- the scene quad warmup attempts to trigger GPU compilation during loading.

### 5. Add URP-Specific Runtime Warmup Coverage

Extend runtime warmup to include material copies for known URP/Open Brush runtime states:

- `SELECTION_ON`,
- audio-reactive keyword states where supported,
- shader scripting/clipping states where supported,
- baked/non-baked export states where relevant.

Do not add all theoretical URP variants blindly. Use the inventory/audit step to keep this based on real Open Brush usage.

### 6. Validate In Player Builds

Add a debug-only shader compile logging mode for player validation:

- enable Unity shader compile logging for test builds where practical,
- capture first-use shader compilation spikes during startup and initial brush selection,
- record which brush/material triggered late compilation.

Validation should include:

- clean install/cold shader cache,
- normal startup,
- brush catalog open,
- selecting a representative set of URP brush shader families,
- selection/highlight on strokes,
- audio-reactive state if supported,
- imported/baked stroke rendering.

## Acceptance Criteria

- Open Brush brush shaders have a generated `ShaderVariantCollection` checked into the project or generated deterministically before build.
- The collection is referenced from `GraphicsSettings.asset`.
- A validation tool reports missing brush variant coverage.
- Runtime warmup remains functional but is no longer the only mechanism protecting URP brush variants.
- Cold-cache player testing shows no unexpected late brush shader compilation for covered brush families.

## Suggested Implementation Order

1. Add editor inventory report.
2. Generate and preload `OpenBrushBrushVariants.shadervariants`.
3. Add validation/audit command.
4. Extend runtime warmup material states.
5. Run cold-cache player validation and refine coverage.

