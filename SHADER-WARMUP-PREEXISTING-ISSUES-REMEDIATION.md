# Shader Warmup Preexisting Issues Remediation Plan

## Scope

This plan covers shader warmup weaknesses that existed before the URP migration. These are not URP regressions, but they still affect reliability and make URP validation harder.

## Evidence

- `Assets/Prefabs/Loading/ShaderWarmup.prefab` has a disabled camera from the initial project history.
- `Assets/Scripts/Rendering/ShaderWarmup.cs` has historically warmed shaders by creating quads rather than by using a brush shader variant collection.
- The existing code only creates extra `SELECTION_ON` material copies. Other runtime states were never comprehensively represented.
- The material comparer groups by shader, shader keywords, and global illumination flags, but not by all render state or runtime property differences that may affect generated variants.

## Preexisting Issues

1. Warmup relies on scene rendering behavior that is not explicit.

   The warmup prefab includes a camera, but the camera is disabled. The system relies on another active camera rendering the generated quads.

2. Warmup has no instrumentation.

   There is no clear log of which materials were warmed, how many variants were represented, whether any material failed, or whether rendering actually happened.

3. Warmup coverage is keyword-limited.

   `SELECTION_ON` is handled specially, but other runtime keyword states are not documented in the warmup system.

4. Warmup object lifecycle is opaque.

   The root object disables itself after warmup. There is no explicit status object/report that can be inspected after startup.

5. Material grouping may hide meaningful differences.

   The comparer intentionally collapses materials by shader, keywords, and GI flags. That reduces work, but can hide differences where material state affects generated variants or rendering behavior.

## Remediation Plan

### 1. Make Rendering Responsibility Explicit

Decide whether `ShaderWarmup` should use:

- its own enabled camera rendering to a small temporary `RenderTexture`, or
- the main scene/loading camera.

Preferred fix: give `ShaderWarmup` explicit ownership of a hidden warmup camera and target texture. This makes the warmup independent of startup camera order and XR camera state.

Implementation notes:

- enable or create a private camera only during warmup,
- render to a small temporary render texture,
- cull only a dedicated warmup layer if possible,
- release the render texture after warmup,
- keep user-visible loading overlay unaffected.

### 2. Add Warmup Logging With A Unique Prefix

Add concise logs with a stable prefix, for example:

`[OB_SHADER_WARMUP]`

Log:

- start/end timestamps,
- number of source materials,
- number of generated material copies,
- number of distinct warmed material states,
- skipped/null materials,
- camera/render target path used,
- total frames used.

Keep logs concise in normal builds. Add verbose detail behind a debug flag.

### 3. Record A Warmup Report In Memory

Create a small runtime report object or struct exposed from `ShaderWarmup.Instance`:

- `MaterialCount`,
- `DistinctMaterialStateCount`,
- `GeneratedSelectionVariantCount`,
- `SkippedMaterialCount`,
- `UsedDedicatedCamera`,
- `Completed`,
- `Error`.

This lets `LoadingScene` and test code verify that warmup completed meaningfully instead of only checking object active state.

### 4. Document Runtime Keyword Coverage

Add a small table in code comments or a project document listing which runtime states are intentionally covered by warmup and which are not.

Start with:

- `SELECTION_ON`: currently covered.
- shader scripting/clipping: evaluate and document.
- audio-reactive: evaluate and document.
- import/export baked variants: evaluate and document.

This should be independent of URP. It documents Open Brush behavior rather than pipeline behavior.

### 5. Revisit Material Distinctness Rules

Review whether `MaterialComparer` should include additional state for non-URP correctness:

- render queue,
- pass/shader tag relevant state,
- enabled instancing,
- key material properties that alter shader branches without keywords.

Do this conservatively. Avoid exploding warmup count unless there is evidence that a state changes compiled shader behavior or first-use cost.

### 6. Add A Runtime Smoke Test

Create an editor/playmode smoke test or debug command that:

- starts warmup,
- waits for completion,
- asserts that `Completed` is true,
- asserts material counts are non-zero,
- asserts a render path was used,
- checks for shader/material errors in Unity logs.

This should not require URP-specific assumptions. It validates the original warmup contract.

## Acceptance Criteria

- Warmup rendering is explicit and does not depend on accidental visibility to another camera.
- Warmup emits concise logs with `[OB_SHADER_WARMUP]`.
- `ShaderWarmup` exposes a completion/report state that tests can inspect.
- Runtime keyword coverage is documented.
- A smoke test or debug command can verify warmup completion and basic material coverage.

## Suggested Implementation Order

1. Add warmup report fields and logging.
2. Make the camera/render-target path explicit.
3. Add smoke test/debug command.
4. Document runtime keyword coverage.
5. Revisit material distinctness only after collecting counts and warmup timing.

